using System.Security.Claims;

namespace RandomRoom.Api.Auth;

public static class PlayerIdentity
{
    public const string NameClaim = "player";

    public static string GetPlayerName(this ClaimsPrincipal user) =>
        user.FindFirstValue(NameClaim) ?? throw new InvalidOperationException("Token has no player claim.");
}
