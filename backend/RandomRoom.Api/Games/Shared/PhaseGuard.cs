using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Shared;

/// <summary>Phase names shared by the round-based games. A game may add its own (e.g. "submitting").</summary>
public static class Phases
{
    public const string Lobby = "lobby";
    public const string Collecting = "collecting";
    public const string Revealed = "revealed";
    public const string Complete = "complete";
}

/// <summary>
/// Allowed phase transitions for one game. Every move goes through here so an illegal one
/// (revealing twice, answering after the reveal) is a RoomRuleException rather than a silent no-op.
/// </summary>
public sealed class PhaseGuard(params (string From, string To)[] allowed)
{
    /// <summary>lobby -> collecting -> revealed -> collecting (next round) or complete.</summary>
    public static readonly PhaseGuard Standard = new(
        (Phases.Lobby, Phases.Collecting),
        (Phases.Collecting, Phases.Revealed),
        (Phases.Revealed, Phases.Collecting),
        (Phases.Revealed, Phases.Complete));

    public string Move(string from, string to)
    {
        if (!allowed.Contains((from, to)))
            throw new RoomRuleException(RuleViolation.Conflict, $"The game cannot go from '{from}' to '{to}' right now.");
        return to;
    }

    /// <summary>Guards an action that is only valid in one phase.</summary>
    public static void Require(string current, string expected, string message)
    {
        if (current != expected)
            throw new RoomRuleException(RuleViolation.Conflict, message);
    }
}
