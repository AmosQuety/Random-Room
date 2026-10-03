namespace RandomRoom.Api.Games.Shared;

/// <summary>What a client needs to render a countdown without ever being trusted with the clock.</summary>
public sealed record TimerView(DateTimeOffset? DeadlineAt, DateTimeOffset ServerNow);

/// <summary>
/// Deadlines are always computed and checked with the server's TimeProvider. Clients get the remaining time
/// only to draw a countdown; when it reaches zero they may send a "tick", which the server honours only if
/// its own clock agrees the deadline has passed.
/// </summary>
public static class ServerTimer
{
    public static DateTimeOffset? DeadlineIn(TimeProvider clock, int? seconds) =>
        seconds is > 0 ? clock.GetUtcNow().AddSeconds(seconds.Value) : null;

    public static bool HasPassed(TimeProvider clock, DateTimeOffset? deadline) =>
        deadline is not null && clock.GetUtcNow() >= deadline.Value;

    public static TimerView View(TimeProvider clock, DateTimeOffset? deadline) => new(deadline, clock.GetUtcNow());
}
