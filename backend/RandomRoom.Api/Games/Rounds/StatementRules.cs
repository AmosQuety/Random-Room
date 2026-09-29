using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

public sealed record StatementPrompt(string Text);

/// <summary>Shared prompt handling for games whose prompt is one line of text.</summary>
public abstract class StatementRules : IRoundRules<StatementPrompt>
{
    private const int MaxPromptLength = 140;

    public abstract string GameType { get; }
    public virtual int MinPlayers => 2;
    public abstract string? BuiltInBank { get; }
    public abstract JsonElement ValidateAnswer(StatementPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw);
    public abstract RoundOutcome Resolve(StatementPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players);

    public StatementPrompt ParsePrompt(JsonElement raw) => new(SetupJson.RequiredText(raw, "text", MaxPromptLength, "The prompt"));

    public object ViewPrompt(StatementPrompt prompt) => new { text = prompt.Text };
}

/// <summary>
/// Everyone votes for one other player. Scoring: the player (or players, on a tie) with the most votes scores 1.
/// The reveal shows how many votes each player got, not who voted for whom.
/// </summary>
public sealed class MostLikelyToRules : StatementRules
{
    public override string GameType => "most-likely-to";

    // Nobody votes for themselves, so with two players each round would be forced.
    public override int MinPlayers => 3;
    public override string? BuiltInBank => "most-likely-to";

    public override JsonElement ValidateAnswer(StatementPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw)
    {
        var chosen = AnswerJson.RequiredText(raw, "player", 40);
        if (chosen == actor)
            throw new RoomRuleException(RuleViolation.InvalidInput, "You cannot vote for yourself.");
        if (!players.Contains(chosen, StringComparer.Ordinal))
            throw new RoomRuleException(RuleViolation.InvalidInput, "Vote for one of the players in this room.");
        return GameStore.ToElement(new { player = chosen });
    }

    public override RoundOutcome Resolve(StatementPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var votes = players.ToDictionary(p => p, _ => 0);
        foreach (var answer in answers)
            votes[answer.Value.GetProperty("player").GetString()!]++;

        var top = votes.Values.Max();
        var winners = top == 0 ? [] : votes.Where(v => v.Value == top).Select(v => v.Key).ToList();
        var tally = votes.Select(v => new { player = v.Key, votes = v.Value }).OrderByDescending(v => v.votes).ToList();

        return new RoundOutcome(new { text = prompt.Text, tally, winners }, winners.ToDictionary(w => w, _ => 1));
    }
}

/// <summary>
/// Everyone says whether they have done the thing. Scoring: "still standing" - a player scores 1 for each
/// statement they have never done, so the scoreboard rewards a quiet life, as the classic game does.
/// </summary>
public sealed class NeverHaveIEverRules : StatementRules
{
    public override string GameType => "never-have-i-ever";
    public override string? BuiltInBank => "never-have-i-ever";

    public override JsonElement ValidateAnswer(StatementPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw) =>
        GameStore.ToElement(new { have = AnswerJson.RequiredBool(raw, "have") });

    public override RoundOutcome Resolve(StatementPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players)
    {
        var have = answers.Where(a => a.Value.GetProperty("have").GetBoolean()).Select(a => a.Player).ToList();
        var never = answers.Where(a => !a.Value.GetProperty("have").GetBoolean()).Select(a => a.Player).ToList();
        return new RoundOutcome(new { text = prompt.Text, have, never }, never.ToDictionary(p => p, _ => 1));
    }
}
