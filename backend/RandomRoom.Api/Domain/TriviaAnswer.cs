namespace RandomRoom.Api.Domain;

/// <summary>One player's answer to one question in one session. Append-only, like RandomPickerEvent.</summary>
public sealed class TriviaAnswer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public required string TriggeredBy { get; set; }
    public int OptionIndex { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
