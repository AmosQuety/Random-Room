using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

public sealed record IntroPrompt(string Clue, string? Link, List<string> Answers);

/// <summary>
/// Guess the song or movie from a clue the host wrote (an emoji string, a riddle, a description) and an optional
/// https link to a clip that players may open themselves. The server marks each guess against the host's accepted
/// answers, ignoring case, accents and punctuation. Scoring: a correct guess scores 1, and the first correct guess
/// (by the server's receipt order) scores 1 more. The accepted answers are only sent with the reveal.
/// </summary>
public sealed class NameThatRules : IRoundRules<IntroPrompt>
{
    private const int MaxClueLength = 200;
    private const int MaxLinkLength = 300;
    private const int MaxAnswerLength = 80;
    private const int MaxAnswers = 6;

    public string GameType => "name-that";
    public string? BuiltInBank => null;

    public IntroPrompt ParsePrompt(JsonElement raw)
    {
        var clue = SetupJson.RequiredText(raw, "clue", MaxClueLength, "The clue");
        var link = ParseLink(SetupJson.OptionalText(raw, "link", MaxLinkLength, "The link"));

        var answers = SetupJson.OptionalArray(raw, "answers", MaxAnswers, "The accepted answers")
            .Select(a => a.ValueKind == JsonValueKind.String ? a.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, "Accepted answers must be text."))
            .Where(a => a.Length > 0)
            .ToList();
        if (answers.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{clue}' needs at least one accepted answer.");
        if (answers.Any(a => a.Length > MaxAnswerLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Accepted answers can be at most {MaxAnswerLength} characters.");
        if (answers.Any(a => AnswerNormalizer.Normalize(a).Length == 0))
            throw new RoomRuleException(RuleViolation.InvalidInput, "An accepted answer needs letters or numbers.");

        return new IntroPrompt(clue, link, answers);
    }

    /// <summary>Only plain https links are allowed: no other scheme, no embedded credentials.</summary>
    private static string? ParseLink(string link)
    {
        if (link.Length == 0) return null;
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "A link must be a full https:// address.");
        return uri.AbsoluteUri;
    }

    public object ViewPrompt(IntroPrompt prompt) => new { clue = prompt.Clue, link = prompt.Link };

    public JsonElement ValidateAnswer(IntroPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw) =>
        GameStore.ToElement(new { text = AnswerJson.RequiredText(raw, "text", MaxAnswerLength) });

    public RoundOutcome Resolve(IntroPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var results = answers
            .Select(a => (a.Player, Text: a.Value.GetProperty("text").GetString()!))
            .Select(a => (a.Player, a.Text, Correct: AnswerNormalizer.MatchesAny(a.Text, prompt.Answers)))
            .ToList();

        var first = results.FirstOrDefault(r => r.Correct).Player;
        var points = results.Where(r => r.Correct).ToDictionary(r => r.Player, r => r.Player == first ? 2 : 1);

        return new RoundOutcome(
            new
            {
                clue = prompt.Clue,
                accepted = prompt.Answers,
                first,
                results = results.Select(r => new { player = r.Player, text = r.Text, correct = r.Correct }).ToList(),
            },
            points);
    }
}
