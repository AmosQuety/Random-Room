namespace RandomRoom.Api.Domain;

/// <summary>One option the room is choosing between (a title, a game, a person - just a label).</summary>
public sealed class RoomChoice
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>The session these choices belong to; see RoomGameSetup.SessionId.</summary>
    public Guid? SessionId { get; set; }
    public required string Label { get; set; }
    public int Position { get; set; }
}
