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

/// <param name="RecoveryCode">The host's way back in if they forget their PIN. Shown once; only its hash is kept.</param>
public sealed record CreateRoomResult(string Slug, IReadOnlyList<PlayerInvite> Invites, string RecoveryCode);

public sealed record HostRecovery(Guid RoomId, string Player, string InviteToken, string NewRecoveryCode);

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

        var recoveryCode = RecoveryCode.Generate();
        var room = new Room
        {
            RecoveryCodeHash = PinHasher.Hash(RecoveryCode.Normalize(recoveryCode)),
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
            invites.Select(i => new PlayerInvite(i.Player.Name, i.Player.InviteToken!)).ToList(),
            recoveryCode);
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
            throw new RoomRuleException(RuleViolation.InvalidInput, "The host's own seat cannot be reset here. If the host forgot their PIN, they can use their recovery code on the join page.");

        var seat = await db.RoomPlayers.FirstOrDefaultAsync(p => p.RoomId == roomId && p.Name == target, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "There is no such player in this room.");
        var latest = await db.GameSessions.AsNoTracking().Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).FirstAsync(ct);
        if (latest.Status == SessionStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current game before resetting a seat.");

        seat.PinHash = null;
        seat.ClaimedAt = null;
        seat.InviteToken = GenerateToken();
        seat.TokenVersion++;
        db.RoomAuditEvents.Add(AuditEvent(roomId, actor, RoomAuditEvent.SeatReset, target));
        await db.SaveChangesAsync(ct);
        return new PlayerInvite(seat.Name, seat.InviteToken);
    }

    /// <summary>Makes a new recovery code for the room, replacing the old one. Host only; the code is returned once.</summary>
    public async Task<string> MakeRecoveryCodeAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can make a recovery code.");

        var code = RecoveryCode.Generate();
        room.RecoveryCodeHash = PinHasher.Hash(RecoveryCode.Normalize(code));
        db.RoomAuditEvents.Add(AuditEvent(roomId, actor, RoomAuditEvent.RecoveryCodeMade, room.HostPlayer));
        await db.SaveChangesAsync(ct);
        return code;
    }

    /// <summary>
    /// For a host who forgot their PIN and is signed out: the recovery code empties the host's seat, signs out
    /// whatever device held it, and returns a one-time link to choose a new PIN. The code is spent and replaced by a
    /// new one in the same step, so a host is never left without one and an old code never works twice.
    /// </summary>
    public async Task<HostRecovery> RecoverHostSeatAsync(string slug, string code, CancellationToken ct = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Slug == slug, ct);

        // A room with no code still pays for a hash comparison, so the answer does not take less time.
        var matches = PinHasher.Verify(RecoveryCode.Normalize(code), room?.RecoveryCodeHash ?? AbsentCodeHash.Value);
        if (!matches || room is null || room.RecoveryCodeHash is null)
            throw new RoomRuleException(RuleViolation.Forbidden, "That recovery code is not right.");

        var newCode = RecoveryCode.Generate();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Spend the code only if nobody else has since the check above, so two uses at once cannot both succeed.
        var spent = await db.Rooms
            .Where(r => r.Id == room.Id && r.RecoveryCodeHash == room.RecoveryCodeHash)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RecoveryCodeHash, PinHasher.Hash(RecoveryCode.Normalize(newCode))), ct);
        if (spent == 0)
            throw new RoomRuleException(RuleViolation.Conflict, "That recovery code was just used. Use the new one.");

        var seat = await db.RoomPlayers.FirstAsync(p => p.RoomId == room.Id && p.Name == room.HostPlayer, ct);
        seat.PinHash = null;
        seat.ClaimedAt = null;
        seat.InviteToken = GenerateToken();
        seat.TokenVersion++;
        // The actor is the code, not a person: nobody was signed in.
        db.RoomAuditEvents.Add(AuditEvent(room.Id, "recovery code", RoomAuditEvent.HostRecovered, room.HostPlayer));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new HostRecovery(room.Id, seat.Name, seat.InviteToken, newCode);
    }

    private static class AbsentCodeHash
    {
        public static readonly string Value = PinHasher.Hash("no recovery code");
    }

    private RoomAuditEvent AuditEvent(Guid roomId, string actor, string action, string target) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        Actor = actor,
        Action = action,
        Target = target,
        OccurredAt = clock.GetUtcNow(),
    };

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
