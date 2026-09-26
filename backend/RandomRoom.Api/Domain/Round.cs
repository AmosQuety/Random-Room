namespace RandomRoom.Api.Domain;

public enum RoundStatus
{
    Waiting,
    Active,
    Completed,
}

public sealed class Round
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public RoundStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}
