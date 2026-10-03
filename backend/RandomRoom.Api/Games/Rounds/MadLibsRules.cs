using System.Text.Json;
using System.Text.RegularExpressions;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

/// <summary>A story with blanks written as {word type}, for example "The {adjective} {animal} ran home."</summary>
public sealed record MadLibPrompt(string Title, string Text);

/// <summary>
/// Players fill in the blanks of a story they cannot see, then the finished story is revealed. Blanks are shared
/// out round-robin in player order (with fewer blanks than players, a blank is asked of several players and the
/// first player in order who answered supplies it). Scoring: 1 point for filling in your words. The story text is
/// only sent with the reveal; players see just the word types they were asked for.
/// </summary>
public sealed partial class MadLibsRules : IRoundRules<MadLibPrompt>
{
    public const int MinBlanks = 2;
    public const int MaxBlanks = 12;
    public const int MaxTextLength = 600;
    public const int MaxTitleLength = 60;
    public const int MaxLabelLength = 24;
    public const int MaxWordLength = 30;

    public string GameType => "mad-libs";
    public string? BuiltInBank => "mad-libs";

    [GeneratedRegex(@"\{([^{}\r\n]*)\}")]
    private static partial Regex Slot();

    public MadLibPrompt ParsePrompt(JsonElement raw)
    {
        var title = SetupJson.RequiredText(raw, "title", MaxTitleLength, "The title");
        var text = SetupJson.RequiredText(raw, "text", MaxTextLength, "The story");
        var labels = Labels(text);
        if (labels.Count < MinBlanks || labels.Count > MaxBlanks)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{title}' needs {MinBlanks} to {MaxBlanks} blanks, written like {{noun}}.");
        if (labels.Any(l => l.Length == 0 || l.Length > MaxLabelLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Each blank needs a word type of 1 to {MaxLabelLength} characters, like {{adjective}}.");
        return new MadLibPrompt(title, text);
    }

    public static List<string> Labels(string text) => Slot().Matches(text).Select(m => m.Groups[1].Value.Trim()).ToList();

    /// <summary>The blanks a player is asked for. Same rule on every path, so the screen and the marking agree.</summary>
    public static IReadOnlyList<int> Assigned(int playerIndex, int playerCount, int blankCount) =>
        blankCount >= playerCount
            ? Enumerable.Range(0, blankCount).Where(b => b % playerCount == playerIndex).ToList()
            : [playerIndex % blankCount];

    public object ViewPrompt(MadLibPrompt prompt) => new { labels = Labels(prompt.Text), assigned = new Dictionary<string, IReadOnlyList<int>>() };

    public object ViewPromptFor(MadLibPrompt prompt, IReadOnlyList<string> players)
    {
        var labels = Labels(prompt.Text);
        return new
        {
            labels,
            assigned = players.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => Assigned(x.i, players.Count, labels.Count)),
        };
    }

    public JsonElement ValidateAnswer(MadLibPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw)
    {
        var expected = Assigned(players.ToList().IndexOf(actor), players.Count, Labels(prompt.Text).Count).Count;
        var words = SetupJson.OptionalArray(raw, "words", MaxBlanks, "The words")
            .Select(w => w.ValueKind == JsonValueKind.String ? w.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, "Each word must be text."))
            .ToList();
        if (words.Count != expected)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Fill in all {expected} of your {(expected == 1 ? "blank" : "blanks")}.");
        if (words.Any(w => w.Length == 0))
            throw new RoomRuleException(RuleViolation.InvalidInput, "A word cannot be empty.");
        if (words.Any(w => w.Length > MaxWordLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Words can be at most {MaxWordLength} characters.");
        return GameStore.ToElement(new { words });
    }

    public RoundOutcome Resolve(MadLibPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var labels = Labels(prompt.Text);
        var byPlayer = answers.ToDictionary(a => a.Player, a => a.Value.GetProperty("words").EnumerateArray().Select(w => w.GetString()!).ToList());

        // For each blank, the first player (in player order) who was asked for it and answered.
        var filled = new (string Word, string By)?[labels.Count];
        for (var i = 0; i < players.Count; i++)
        {
            if (!byPlayer.TryGetValue(players[i], out var words)) continue;
            var blanks = Assigned(i, players.Count, labels.Count);
            for (var k = 0; k < blanks.Count; k++)
                filled[blanks[k]] ??= (words[k], players[i]);
        }

        var parts = new List<object>();
        var last = 0;
        var index = 0;
        foreach (Match match in Slot().Matches(prompt.Text))
        {
            if (match.Index > last) parts.Add(new { text = prompt.Text[last..match.Index] });
            var slot = filled[index];
            parts.Add(new { word = slot?.Word ?? "...", by = slot?.By, label = labels[index] });
            last = match.Index + match.Length;
            index++;
        }
        if (last < prompt.Text.Length) parts.Add(new { text = prompt.Text[last..] });

        return new RoundOutcome(new { title = prompt.Title, parts }, answers.ToDictionary(a => a.Player, _ => 1));
    }
}
