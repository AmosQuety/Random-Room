using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Auth;

/// <summary>Verifies a player's secret PIN and issues a short-lived signed token.</summary>
public sealed class PlayerTokenService(IOptions<RoomOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static SymmetricSecurityKey SigningKey(RoomOptions options) =>
        new(Encoding.UTF8.GetBytes(options.JwtSigningKey));

    public string? TryIssueToken(string player, string pin)
    {
        return PinMatches(player, pin) ? CreateToken(player) : null;
    }

    private bool PinMatches(string player, string pin)
    {
        // Unknown players still get a comparison so timing does not reveal valid names.
        var expected = RoomDefinition.IsPlayer(player) ? options.Value.PlayerPins[player] : new string('x', 32);
        var known = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pin), Encoding.UTF8.GetBytes(expected));
        return known && RoomDefinition.IsPlayer(player);
    }

    private string CreateToken(string player)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(PlayerIdentity.NameClaim, player)]),
            NotBefore = now,
            Expires = now + Lifetime,
            SigningCredentials = new SigningCredentials(SigningKey(options.Value), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
