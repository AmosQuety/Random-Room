namespace RandomRoom.Api.Domain;

/// <summary>One host-curated question. Per session: a new game of the same kind copies the previous session's set.</summary>
public sealed class TriviaQuestion
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>The session this question belongs to; see RoomGameSetup.SessionId.</summary>
    public Guid? SessionId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public required List<string> Options { get; set; }
    public int CorrectIndex { get; set; }

    /// <summary>Optional label such as "Science", shown as a chip on the question.</summary>
    public string? Category { get; set; }
}
