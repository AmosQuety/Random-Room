namespace RandomRoom.Api.Domain;

/// <summary>A user-created decision room: a title, a set of choices, and named player slots.</summary>
public sealed class Room
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }

    /// <summary>The RoomPlayer.Name who controls round lifecycle. Defaults to the room's creator.</summary>
    public required string HostPlayer { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
