using System.Text.Json;
using RandomRoom.Api.Games.Shared;

namespace RandomRoom.Api.Games.Rounds;

/// <summary>The game-private part of a round game's session state, stored as JSON on GameSessionState.</summary>
public sealed class RoundData
{
    /// <summary>Indexes into the setup's prompts, in the order this session plays them.</summary>
    public List<int> Order { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    /// <summary>The revealed summary of the current round. Null until the round is revealed.</summary>
    public JsonElement? Result { get; set; }
}

public sealed record RoundScore(string Player, int Score);

/// <summary>
/// What a viewer sees. Answers are never in here while a round is collecting: only who has answered, and the
/// viewer's own answer. Result is present only once the round is revealed.
/// </summary>
public sealed record RoundPayload(
    string Phase,
    int Round,
    int TotalRounds,
    object? Prompt,
    IReadOnlyDictionary<string, bool> Answered,
    JsonElement? MyAnswer,
    JsonElement? Result,
    IReadOnlyList<RoundScore> Scoreboard,
    TimerView Timer,
    int? TimeLimitSeconds);

public sealed record RoundPreview(int PromptCount, int Rounds, int? TimeLimitSeconds);
