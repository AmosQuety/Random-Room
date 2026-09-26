namespace RandomRoom.Api.Services;

public enum RuleViolation
{
    Forbidden,
    Conflict,
}

/// <summary>A request that is well-formed but not allowed by the room rules.</summary>
public sealed class RoomRuleException(RuleViolation violation, string message) : Exception(message)
{
    public RuleViolation Violation { get; } = violation;
}
