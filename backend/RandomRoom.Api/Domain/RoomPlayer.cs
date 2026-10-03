namespace RandomRoom.Api.Domain;

/// <summary>
/// A named slot in a room. Created empty (no PIN) with an invite token; the invited person
/// claims it themselves by setting their own PIN, so nobody else - including the host - ever learns it.
/// </summary>
public sealed class RoomPlayer
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public required string Name { get; set; }

    public string? PinHash { get; set; }
    public string? InviteToken { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>
    /// Part of every token issued for this seat. Resetting the seat raises it, so tokens issued before the reset
    /// stop working at once instead of lasting out their lifetime.
    /// </summary>
    public int TokenVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsClaimed => ClaimedAt is not null;
}
