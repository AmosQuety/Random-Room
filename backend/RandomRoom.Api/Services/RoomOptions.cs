using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

public sealed class RoomOptions
{
    public const string SectionName = "Room";

    public string HostPlayer { get; set; } = "Amos";

    /// <summary>Secret per-player join code. Supplied by configuration or environment, never committed.</summary>
    public Dictionary<string, string> PlayerPins { get; set; } = new();

    public string JwtSigningKey { get; set; } = "";

    public static bool IsValid(RoomOptions options) =>
        RoomDefinition.IsPlayer(options.HostPlayer)
        && RoomDefinition.Players.All(p => options.PlayerPins.TryGetValue(p, out var pin) && pin.Length >= 4)
        && options.JwtSigningKey.Length >= 32;
}
