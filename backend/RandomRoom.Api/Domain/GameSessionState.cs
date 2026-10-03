namespace RandomRoom.Api.Domain;

/// <summary>
/// Progress of one session through a round-based game: the phase, the round index, an optional
/// server-stamped deadline, and the game's own private state as JSON. One row per GameSession.
/// Version is an optimistic concurrency token, so two racing requests cannot both advance the same round.
/// </summary>
public sealed class GameSessionState
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string Phase { get; set; }
    public int Round { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }

    /// <summary>The player who won the current round's first-come claim (buzzer). Cleared when the round advances.</summary>
    public string? Claimant { get; set; }

    public required string DataJson { get; set; }
    public int Version { get; set; }
}
