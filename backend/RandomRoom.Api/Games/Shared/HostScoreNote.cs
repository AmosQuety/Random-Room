namespace RandomRoom.Api.Games.Shared;

/// <summary>
/// A score change (or a decision not to change one) that the host made, kept so everyone can see it. A host who also
/// plays could otherwise tilt a score without anyone being able to tell. Public by design: it only names who, how many
/// points, and why.
/// </summary>
public sealed record HostScoreNote(int Round, string Player, int Points, string Reason);
