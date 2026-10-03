namespace RandomRoom.Api.Domain;

/// <summary>
/// One thing a player did in a round (an answer, a vote, a buzz, a stroke). Kind names the game's own
/// meaning. The unique (session, round, kind, player, seq) index makes "one answer per player per prompt"
/// a database guarantee; games that allow several entries per round use Seq. Ordinal is assigned by the
/// database on insert, so it gives a server-side receipt order that the client can never influence.
/// </summary>
public sealed class GameEntry
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int Round { get; set; }
    public required string Kind { get; set; }
    public required string Player { get; set; }
    public int Seq { get; set; }
    public required string ValueJson { get; set; }
    public DateTimeOffset ServerTime { get; set; }
    public long Ordinal { get; set; }
}
