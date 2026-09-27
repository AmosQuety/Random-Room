using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RandomRoom.Api.Data;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Auth;

public sealed record IssuedToken(string Token, Guid RoomId, string RoomSlug, bool IsHost);

/// <summary>Verifies a claimed player's PIN against their own hash and issues a short-lived signed token.</summary>
public sealed class PlayerTokenService(RoomDbContext db, IOptions<RoomOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static SymmetricSecurityKey SigningKey(RoomOptions options) =>
        new(Encoding.UTF8.GetBytes(options.JwtSigningKey));

    public async Task<IssuedToken?> TryIssueTokenAsync(string roomSlug, string player, string pin, CancellationToken ct)
    {
        var match = await db.RoomPlayers
            .Where(p => p.Room!.Slug == roomSlug && p.Name == player)
            .Select(p => new { p.RoomId, p.PinHash, HostPlayer = p.Room!.HostPlayer })
            .SingleOrDefaultAsync(ct);

        // A claimed-but-nonexistent player still runs a hash comparison so timing does not reveal valid names.
        var known = match is not null && PinHasher.Verify(pin, match.PinHash);
        if (!known) return null;

        var token = CreateToken(match!.RoomId, player);
        return new IssuedToken(token, match.RoomId, roomSlug, player == match.HostPlayer);
    }

    private string CreateToken(Guid roomId, string player)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(PlayerIdentity.NameClaim, player),
                new Claim(PlayerIdentity.RoomClaim, roomId.ToString()),
            ]),
            NotBefore = now,
            Expires = now + Lifetime,
            SigningCredentials = new SigningCredentials(SigningKey(options.Value), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
