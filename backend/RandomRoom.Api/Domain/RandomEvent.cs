namespace RandomRoom.Api.Domain;

/// <summary>
/// One system-made random selection. Append-only: the DbContext rejects updates and deletes.
/// </summary>
public sealed class RandomEvent
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Round? Round { get; set; }
    public required string TriggeredBy { get; set; }
    public required string Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
