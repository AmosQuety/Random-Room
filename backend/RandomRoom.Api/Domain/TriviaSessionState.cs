namespace RandomRoom.Api.Domain;

/// <summary>Per-session progress through the room's question set. One row per GameSession.</summary>
public sealed class TriviaSessionState
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int CurrentQuestionIndex { get; set; }

    /// <summary>When the current question closes, or null when the room has no time limit.</summary>
    public DateTimeOffset? DeadlineAt { get; set; }
}
