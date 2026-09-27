namespace RandomRoom.Api.Games.RandomPicker;

public sealed record RandomPickerPlayerState(bool HasTriggered, string? Result);

public sealed record TallyView(string Choice, int Count);

public sealed record ActivityView(Guid EventId, Guid SessionId, int SessionNumber, string TriggeredBy, string Result, DateTimeOffset Timestamp);

public sealed record RandomPickerPayload(
    IReadOnlyList<string> Choices,
    IReadOnlyDictionary<string, RandomPickerPlayerState> Players,
    IReadOnlyList<TallyView> Tally,
    IReadOnlyList<ActivityView> Activity);
