namespace RandomRoom.Api.Domain;

/// <summary>
/// One validated setup document per room, for games that keep their whole setup as JSON
/// (prompt lists, word lists, story templates). Room-level: every session replays the same setup.
/// </summary>
public sealed class RoomGameSetup
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public required string Json { get; set; }
}
