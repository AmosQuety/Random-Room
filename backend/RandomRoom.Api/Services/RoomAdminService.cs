using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.RandomPicker;

namespace RandomRoom.Api.Services;

public sealed record CreateRoomRequest(string Title, IReadOnlyList<string> Choices, IReadOnlyList<string> Players, string HostPlayer);

public sealed record PlayerInvite(string Player, string InviteToken);

public sealed record CreateRoomResult(string Slug, IReadOnlyList<PlayerInvite> Invites);

public sealed record RoomPreviewPlayer(string Name, bool Claimed);

public sealed record RoomPreview(string Title, IReadOnlyList<string> Choices, IReadOnlyList<RoomPreviewPlayer> Players);

/// <summary>
/// Room lifecycle outside of gameplay: creating a room and letting invited players claim their
/// own slot. A player's PIN is set by that player alone during claim - the host never sees it.
/// </summary>
public sealed class RoomAdminService(RoomDbContext db, TimeProvider clock, IEnumerable<IGameEngine> engines)
{
    private const int MinChoices = 2;
    private const int MinPlayers = 2;

    public async Task<CreateRoomResult> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var choices = request.Choices.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        var players = request.Players.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();

        if (title.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "A room needs a title.");
        if (choices.Count < MinChoices)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A room needs at least {MinChoices} choices.");
        if (players.Count < MinPlayers)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A room needs at least {MinPlayers} players.");
        if (!players.Contains(request.HostPlayer, StringComparer.Ordinal))
            throw new RoomRuleException(RuleViolation.InvalidInput, "The host must be one of the room's players.");

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Slug = await GenerateUniqueSlugAsync(ct),
            Title = title,
            GameType = RandomPickerEngine.Key, // only game type today; room creation will offer a choice once a second one exists
            HostPlayer = request.HostPlayer,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Rooms.Add(room);

        db.RoomChoices.AddRange(choices.Select((label, i) => new RoomChoice
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Label = label,
            Position = i,
        }));

        var invites = players.Select(name => new
        {
            Player = new RoomPlayer
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Name = name,
                InviteToken = GenerateToken(),
                CreatedAt = clock.GetUtcNow(),
            },
        }).ToList();
        db.RoomPlayers.AddRange(invites.Select(i => i.Player));

        var firstSession = new GameSession
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Number = 1,
            Status = SessionStatus.Waiting,
            CreatedAt = clock.GetUtcNow(),
        };
        db.GameSessions.Add(firstSession);

        var engine = engines.FirstOrDefault(e => e.GameType == room.GameType)
            ?? throw new InvalidOperationException($"No IGameEngine registered for game type '{room.GameType}'.");
        await engine.OnSessionCreatedAsync(room.Id, firstSession.Id, ct);

        await db.SaveChangesAsync(ct);

        return new CreateRoomResult(
            room.Slug,
            invites.Select(i => new PlayerInvite(i.Player.Name, i.Player.InviteToken!)).ToList());
    }

    public async Task<RoomPreview> GetPreviewAsync(string slug, CancellationToken ct = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Slug == slug, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");

        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == room.Id).OrderBy(c => c.Position).Select(c => c.Label).ToListAsync(ct);
        var players = await db.RoomPlayers.AsNoTracking()
            .Where(p => p.RoomId == room.Id)
            .Select(p => new RoomPreviewPlayer(p.Name, p.ClaimedAt != null))
            .ToListAsync(ct);

        return new RoomPreview(room.Title, choices, players);
    }

    public async Task<string> ClaimInviteAsync(string slug, string inviteToken, string pin, CancellationToken ct = default)
    {
        var player = await db.RoomPlayers
            .FirstOrDefaultAsync(p => p.Room!.Slug == slug && p.InviteToken == inviteToken, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "This invite link is invalid or already used.");

        player.PinHash = PinHasher.Hash(pin);
        player.InviteToken = null;
        player.ClaimedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return player.Name;
    }

    private async Task<string> GenerateUniqueSlugAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var slug = GenerateToken(6).ToLowerInvariant();
            if (!await db.Rooms.AnyAsync(r => r.Slug == slug, ct)) return slug;
        }
        throw new InvalidOperationException("Could not generate a unique room slug.");
    }

    private static string GenerateToken(int byteCount = 24) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
