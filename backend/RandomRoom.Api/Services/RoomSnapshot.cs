using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

/// <summary>Everything a client needs to render the room. The same view is sent to every participant.</summary>
public sealed record RoomSnapshot(
    string RoomId,
    string RoomName,
    string HostPlayer,
    IReadOnlyList<Choice> Choices,
    RoundView Round,
    IReadOnlyList<PlayerView> Players,
    IReadOnlyList<TallyView> Tally,
    IReadOnlyList<ActivityView> Activity);

public sealed record RoundView(Guid Id, int Number, RoundStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

public sealed record PlayerView(string Name, bool Online, bool HasTriggered, string? Result);

public sealed record TallyView(string Choice, int Count);

public sealed record ActivityView(
    Guid EventId,
    string RoomId,
    Guid RoundId,
    int RoundNumber,
    string TriggeredBy,
    string Result,
    DateTimeOffset Timestamp);
