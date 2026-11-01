using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

/// <summary>
/// Everything a client needs to render the room, built per viewer: GamePayload is the game's view for that one player
/// (hidden information such as a secret word or a card is stripped for everyone not entitled to it).
/// GamePayload is whatever shape the room's IGameEngine returns - generic here on purpose.
/// HostPlays is false when the host referees and is not one of the participants.
/// Sequence orders snapshots of one room: a client keeps the highest it has seen and ignores older ones.
/// </summary>
public sealed record RoomSnapshot(
    Guid RoomId,
    string RoomSlug,
    string RoomTitle,
    string HostPlayer,
    bool HostPlays,
    string GameType,
    SessionView Session,
    IReadOnlyList<PlayerView> Players,
    object GamePayload,
    long Sequence);

/// <summary>One session. GameType is the game this session plays; the room's GameType is the current game.</summary>
public sealed record SessionView(Guid Id, int Number, SessionStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string GameType);

/// <summary>
/// Room-level presence only; whatever a player has done *in* the game lives in GamePayload.
/// Claimed is false until the player has opened their invite and set a PIN, which tells the group who is still missing.
/// Plays is false only for a host who referees.
/// </summary>
public sealed record PlayerView(string Name, bool Online, bool Claimed, bool Plays);
