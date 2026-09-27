using System.Security.Claims;

namespace RandomRoom.Api.Auth;

public static class PlayerIdentity
{
    public const string NameClaim = "player";
    public const string RoomClaim = "room";

    public static string GetPlayerName(this ClaimsPrincipal user) =>
        user.FindFirstValue(NameClaim) ?? throw new InvalidOperationException("Token has no player claim.");

    public static Guid GetRoomId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(RoomClaim), out var roomId)
            ? roomId
            : throw new InvalidOperationException("Token has no room claim.");
}
