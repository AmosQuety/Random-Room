namespace RandomRoom.Api.Domain;

/// <summary>One host-curated question, set at room creation. Room-level: every session in the room replays the same set.</summary>
public sealed class TriviaQuestion
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public required List<string> Options { get; set; }
    public int CorrectIndex { get; set; }

    /// <summary>Optional label such as "Science", shown as a chip on the question.</summary>
    public string? Category { get; set; }
}
