using System.Security.Claims;

namespace RandomRoom.Api.Auth;

public static class PlayerIdentity
{
    public const string NameClaim = "player";
    public const string RoomClaim = "room";

    /// <summary>The seat's TokenVersion when the token was issued; a token whose version is behind is no longer valid.</summary>
    public const string VersionClaim = "ver";

    /// <summary>Tokens issued before versions existed have no claim and count as version 0, the seat's starting value.</summary>
    public static int GetTokenVersion(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(VersionClaim), out var version) ? version : 0;

    public static string GetPlayerName(this ClaimsPrincipal user) =>
        user.FindFirstValue(NameClaim) ?? throw new InvalidOperationException("Token has no player claim.");

    public static Guid GetRoomId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(RoomClaim), out var roomId)
            ? roomId
            : throw new InvalidOperationException("Token has no room claim.");
}
