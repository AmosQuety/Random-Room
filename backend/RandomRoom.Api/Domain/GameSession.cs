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
    public SessionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}
