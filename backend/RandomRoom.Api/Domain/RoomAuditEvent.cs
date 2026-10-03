namespace RandomRoom.Api.Domain;

/// <summary>
/// An append-only record of a sensitive action in a room: who did what to whom, and when. Kept apart from game
/// state and from operational logs, and never updated or deleted by the application while the room exists.
/// </summary>
public sealed class RoomAuditEvent
{
    public const string SeatReset = "seat-reset";
    public const string HostRecovered = "host-recovered";
    public const string RecoveryCodeMade = "recovery-code-made";

    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Target { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
