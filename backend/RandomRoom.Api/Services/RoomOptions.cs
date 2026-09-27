namespace RandomRoom.Api.Services;

public sealed class RoomOptions
{
    public const string SectionName = "Room";

    /// <summary>Deployment-wide token signing secret. Supplied by configuration or environment, never committed.</summary>
    public string JwtSigningKey { get; set; } = "";

    public static bool IsValid(RoomOptions options) => options.JwtSigningKey.Length >= 32;
}
