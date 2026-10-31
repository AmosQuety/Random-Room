namespace RandomRoom.Api.Domain;

/// <summary>A user-created decision room: a title, a set of choices, and named player slots.</summary>
public sealed class Room
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }

    /// <summary>The current game: always equal to the latest session's GameType, which is what really runs.</summary>
    public required string GameType { get; set; }

    /// <summary>The RoomPlayer.Name who controls session lifecycle. Defaults to the room's creator.</summary>
    public required string HostPlayer { get; set; }

    /// <summary>
    /// Hash of the host's recovery code, which lets them back in if they forget their PIN. Null for rooms made before
    /// recovery codes existed, until the host makes one.
    /// </summary>
    public string? RecoveryCodeHash { get; set; }

    /// <summary>
    /// Whether the host also plays. When false the host is a referee: they run the game but are not a participant.
    /// Rooms made before referees existed are true.
    /// </summary>
    public bool HostPlays { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
