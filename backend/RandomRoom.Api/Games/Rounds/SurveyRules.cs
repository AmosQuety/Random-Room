using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

public sealed record SurveyAnswer(string Text, int Points, List<string> Aliases);

public sealed record SurveyPrompt(string Text, List<SurveyAnswer> Answers);

/// <summary>
/// A survey question with a hidden board of answers worth different points. Everyone types one guess; a guess
/// that matches a board answer (ignoring case, accents and punctuation, or any listed alias) scores that
/// answer's points. The board is only sent with the reveal. Individual play rather than teams.
/// </summary>
public sealed class SurveyShowdownRules : IRoundRules<SurveyPrompt>
{
    private const int MaxTextLength = 140;
    private const int MaxGuessLength = 60;
    private const int MaxAnswers = 8;

    public string GameType => "survey-showdown";
    public string? BuiltInBank => "survey-showdown";

    public SurveyPrompt ParsePrompt(JsonElement raw)
    {
        var text = SetupJson.RequiredText(raw, "text", MaxTextLength, "The question");
        var answers = SetupJson.OptionalArray(raw, "answers", MaxAnswers, "The answer list").Select(ParseAnswer).ToList();
        if (answers.Count < 2)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs at least 2 board answers.");
        return new SurveyPrompt(text, answers);
    }

    private static SurveyAnswer ParseAnswer(JsonElement raw)
    {
        var text = SetupJson.RequiredText(raw, "text", MaxGuessLength, "A board answer");
        var points = SetupJson.OptionalInt(raw, "points", 1, 100, "Answer points")
            ?? throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs points.");
        var aliases = SetupJson.OptionalArray(raw, "aliases", 6, "Aliases")
            .Select(a => a.ValueKind == JsonValueKind.String ? a.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, "Aliases must be text."))
            .Where(a => a.Length is > 0 and <= MaxGuessLength)
            .ToList();
        return new SurveyAnswer(text, points, aliases);
    }

    public object ViewPrompt(SurveyPrompt prompt) => new { text = prompt.Text, boardSize = prompt.Answers.Count };

    public JsonElement ValidateAnswer(SurveyPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw) =>
        GameStore.ToElement(new { text = AnswerJson.RequiredText(raw, "text", MaxGuessLength) });

    public RoundOutcome Resolve(SurveyPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var guesses = answers.Select(a => (a.Player, Text: a.Value.GetProperty("text").GetString()!, Match: MatchIndex(prompt, a.Value.GetProperty("text").GetString()!))).ToList();

        var board = prompt.Answers
            .Select((answer, i) => new { text = answer.Text, points = answer.Points, guessedBy = guesses.Where(g => g.Match == i).Select(g => g.Player).ToList() })
            .ToList();
        var points = guesses.Where(g => g.Match >= 0).ToDictionary(g => g.Player, g => prompt.Answers[g.Match].Points);

        return new RoundOutcome(
            new { text = prompt.Text, board, guesses = guesses.Select(g => new { player = g.Player, text = g.Text, matched = g.Match >= 0 }).ToList() },
            points);
    }

    private static int MatchIndex(SurveyPrompt prompt, string guess) =>
        prompt.Answers.FindIndex(a => AnswerNormalizer.MatchesAny(guess, a.Aliases.Prepend(a.Text)));
}
