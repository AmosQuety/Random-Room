namespace RandomRoom.Api.Services;

/// <summary>
/// Per-address request limits, per minute. The defaults are the production values; only a test harness that signs
/// many players in from one address should raise them.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";
    public const int DefaultJoinPerMinute = 20;
    public const int DefaultRoomAdminPerMinute = 40;

    /// <summary>Joining and recovering a host seat: the PIN-guessing surface, so the tighter limit.</summary>
    public int JoinPerMinute { get; set; } = DefaultJoinPerMinute;

    /// <summary>Creating rooms, claiming invites and other room administration.</summary>
    public int RoomAdminPerMinute { get; set; } = DefaultRoomAdminPerMinute;

    public static bool IsValid(RateLimitOptions options) => options.JoinPerMinute > 0 && options.RoomAdminPerMinute > 0;
}
