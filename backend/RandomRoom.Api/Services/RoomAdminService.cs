using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using Microsoft.Extensions.Options;
using RandomRoom.Api.Games;

namespace RandomRoom.Api.Services;

public sealed record CreateRoomRequest(string Title, string GameType, JsonElement Setup, IReadOnlyList<string> Players, string HostPlayer);

public sealed record PlayerInvite(string Player, string InviteToken);

public sealed record CreateRoomResult(string Slug, IReadOnlyList<PlayerInvite> Invites);

public sealed record RoomPreviewPlayer(string Name, bool Claimed);

/// <param name="RetentionDays">After how many days without play the room is deleted; 0 when it never is.</param>
public sealed record RoomPreview(string Title, string GameType, object GamePreview, IReadOnlyList<RoomPreviewPlayer> Players, int RetentionDays = 0);

/// <summary>
/// Room lifecycle outside of gameplay: creating a room and letting invited players claim their
/// own slot. A player's PIN is set by that player alone during claim - the host never sees it.
/// Everything about a game's own setup (Random Picker's choices, Trivia's questions) is opaque
/// here - it's validated and persisted by that game's own IGameEngine.
/// </summary>
public sealed class RoomAdminService(RoomDbContext db, TimeProvider clock, IEnumerable<IGameEngine> engines, IOptions<RoomOptions>? options = null)
{
    private const int MinPlayers = 2;

    // Match the column sizes in RoomDbContext, so oversized input is a 400 and not a database error.
    private const int MaxTitleLength = 80;
    private const int MaxPlayerNameLength = 32;

    public async Task<CreateRoomResult> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var players = request.Players.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();

        if (title.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "A room needs a title.");
        if (title.Length > MaxTitleLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The room name is too long (at most {MaxTitleLength} characters).");
        if (players.Any(p => p.Length > MaxPlayerNameLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Player names can be at most {MaxPlayerNameLength} characters.");
        if (players.Count < MinPlayers)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A room needs at least {MinPlayers} players.");
        if (!players.Contains(request.HostPlayer, StringComparer.Ordinal))
            throw new RoomRuleException(RuleViolation.InvalidInput, "The host must be one of the room's players.");

        var engine = EngineFor(request.GameType);
        if (players.Count < engine.MinPlayers)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"This game needs at least {engine.MinPlayers} players.");
        if (players.Count > engine.MaxPlayers)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"This game works with at most {engine.MaxPlayers} players.");

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Slug = await GenerateUniqueSlugAsync(ct),
            Title = title,
            GameType = request.GameType,
            HostPlayer = request.HostPlayer,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Rooms.Add(room);

        await engine.ConfigureRoomAsync(room.Id, request.Setup, ct);

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

        var players = await db.RoomPlayers.AsNoTracking()
            .Where(p => p.RoomId == room.Id)
            .Select(p => new RoomPreviewPlayer(p.Name, p.ClaimedAt != null))
            .ToListAsync(ct);
        var gamePreview = await EngineFor(room.GameType).GetRoomPreviewAsync(room.Id, ct);

        return new RoomPreview(room.Title, room.GameType, gamePreview, players, options?.Value.RetentionDays ?? 0);
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

    /// <summary>Deletes the room and everything in it. Only the host can, and it cannot be undone.</summary>
    public async Task DeleteRoomAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can delete the room.");

        await RoomDeletion.DeleteAsync(db, [roomId], ct);
    }

    /// <summary>
    /// For a player who forgot their PIN or lost their device: empties the seat so its owner can claim it again with a
    /// new one-time link, and signs out whatever device held it. Only the host may do this, never to their own seat
    /// (nobody else could reset it), and never during a game, so the host cannot take over a seat and see its secrets.
    /// </summary>
    public async Task<PlayerInvite> ResetSeatAsync(Guid roomId, string actor, string target, CancellationToken ct = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can reset a seat.");
        if (target == room.HostPlayer)
            throw new RoomRuleException(RuleViolation.InvalidInput, "The host's own seat cannot be reset. If the host forgot their PIN, start a new room.");

        var seat = await db.RoomPlayers.FirstOrDefaultAsync(p => p.RoomId == roomId && p.Name == target, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "There is no such player in this room.");
        var latest = await db.GameSessions.AsNoTracking().Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).FirstAsync(ct);
        if (latest.Status == SessionStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current game before resetting a seat.");

        seat.PinHash = null;
        seat.ClaimedAt = null;
        seat.InviteToken = GenerateToken();
        seat.TokenVersion++;
        db.RoomAuditEvents.Add(new RoomAuditEvent
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Actor = actor,
            Action = RoomAuditEvent.SeatReset,
            Target = target,
            OccurredAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return new PlayerInvite(seat.Name, seat.InviteToken);
    }

    private IGameEngine EngineFor(string gameType) =>
        engines.FirstOrDefault(e => e.GameType == gameType)
        ?? throw new RoomRuleException(RuleViolation.InvalidInput, $"'{gameType}' is not a known game type.");

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
