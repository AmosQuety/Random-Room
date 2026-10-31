namespace RandomRoom.Api.Domain;

public enum SessionStatus
{
    Waiting,
    Active,
    Completed,
}

/// <summary>
/// One played instance of a room's game, e.g. one round of Random Picker or one trivia set.
/// Generic across game types: lifecycle and host authorization live here; what happens
/// while a session is active is entirely up to the room's IGameEngine.
/// </summary>
public sealed class GameSession
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int Number { get; set; }

    /// <summary>
    /// Which IGameEngine runs this session. Copied from the previous session (the room's game at creation). Equal to
    /// Room.GameType today, because nothing can change the game; a "change game" must update both together.
    /// </summary>
    public required string GameType { get; set; }

    public SessionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Whether the host played this game, copied from Room.HostPlays when the session is made. Room.HostPlays is the
    /// setting for the next game; this keeps a finished game's participants as they were.
    /// </summary>
    public bool HostPlays { get; set; } = true;
}
