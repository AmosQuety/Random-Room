namespace RandomRoom.Api.Domain;

/// <summary>
/// One validated setup document per session, for games that keep their whole setup as JSON
/// (prompt lists, word lists, story templates). A new game of the same kind copies the previous session's.
/// </summary>
public sealed class RoomGameSetup
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>The session this setup belongs to. Null only for a room that had setup but no session before the move.</summary>
    public Guid? SessionId { get; set; }
    public required string Json { get; set; }
}
