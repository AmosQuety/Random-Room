using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

/// <summary>
/// Everything a client needs to render the room. The same view is sent to every participant.
/// GamePayload is whatever shape the room's IGameEngine returns - generic here on purpose.
/// Sequence orders snapshots of one room: a client keeps the highest it has seen and ignores older ones.
/// </summary>
public sealed record RoomSnapshot(
    Guid RoomId,
    string RoomSlug,
    string RoomTitle,
    string HostPlayer,
    string GameType,
    SessionView Session,
    IReadOnlyList<PlayerView> Players,
    object GamePayload,
    long Sequence);

public sealed record SessionView(Guid Id, int Number, SessionStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

/// <summary>Room-level presence only; whatever a player has done *in* the game lives in GamePayload.</summary>
public sealed record PlayerView(string Name, bool Online);
