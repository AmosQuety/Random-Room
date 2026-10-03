namespace RandomRoom.Api.Services;

public sealed class RoomOptions
{
    public const string SectionName = "Room";

    /// <summary>Deployment-wide token signing secret. Supplied by configuration or environment, never committed.</summary>
    public string JwtSigningKey { get; set; } = "";

    /// <summary>
    /// Rooms with no activity for this many days are deleted, with everything in them. 0 turns the clean-up off.
    /// Set it to the retention period the deployment is allowed or required to keep data for.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    public static bool IsValid(RoomOptions options) => options.JwtSigningKey.Length >= 32 && options.RetentionDays >= 0;
}
