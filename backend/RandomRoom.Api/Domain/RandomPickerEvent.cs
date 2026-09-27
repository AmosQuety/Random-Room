namespace RandomRoom.Api.Domain;

/// <summary>
/// One system-made random selection, owned by the Random Picker game engine.
/// Append-only: the DbContext rejects updates and deletes.
/// </summary>
public sealed class RandomPickerEvent
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }
    public required string TriggeredBy { get; set; }
    public required string Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
