using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

public sealed record TwoWayPrompt(string A, string B);

/// <summary>
/// Two options, everyone picks one privately. Scoring: a player who picked the majority side scores 1;
/// an even split scores nobody. Shared by Would You Rather and This or That, which differ in tone and content.
/// </summary>
public abstract class TwoWayRules : IRoundRules<TwoWayPrompt>
{
    private const int MaxOptionLength = 100;

    public abstract string GameType { get; }
    public abstract string? BuiltInBank { get; }

    public TwoWayPrompt ParsePrompt(JsonElement raw) =>
        new(SetupJson.RequiredText(raw, "a", MaxOptionLength, "Option A"), SetupJson.RequiredText(raw, "b", MaxOptionLength, "Option B"));

    public object ViewPrompt(TwoWayPrompt prompt) => new { options = new[] { prompt.A, prompt.B } };

    public JsonElement ValidateAnswer(TwoWayPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw)
    {
        var choice = AnswerJson.RequiredInt(raw, "choice");
        if (choice is not (0 or 1))
            throw new RoomRuleException(RuleViolation.InvalidInput, "Pick one of the two options.");
        return GameStore.ToElement(new { choice });
    }

    public RoundOutcome Resolve(TwoWayPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var voters = new[] { new List<string>(), new List<string>() };
        foreach (var answer in answers)
            voters[answer.Value.GetProperty("choice").GetInt32()].Add(answer.Player);

        int? majority = voters[0].Count == voters[1].Count ? null : voters[0].Count > voters[1].Count ? 0 : 1;
        var points = majority is null ? [] : voters[majority.Value].ToDictionary(p => p, _ => 1);

        return new RoundOutcome(
            new { options = new[] { prompt.A, prompt.B }, voters, counts = voters.Select(v => v.Count).ToArray(), majority },
            points);
    }
}

public sealed class WouldYouRatherRules : TwoWayRules
{
    public override string GameType => "would-you-rather";
    public override string? BuiltInBank => "would-you-rather";
}

public sealed class ThisOrThatRules : TwoWayRules
{
    public override string GameType => "this-or-that";
    public override string? BuiltInBank => "this-or-that";
}
