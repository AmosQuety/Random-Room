using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;

namespace RandomRoom.Api.Services;

/// <summary>
/// The generic room/session lifecycle, shared by every game type: who's the host, is the
/// session active, when a new one starts. Everything about what happens inside an active
/// session is delegated to the room's IGameEngine (chosen by Room.GameType).
/// </summary>
public sealed class GameSessionService(
    RoomDbContext db, PresenceTracker presence, TimeProvider clock, IEnumerable<IGameEngine> engines, RoomSnapshotSequencer? sequencer = null)
{
    private readonly RoomSnapshotSequencer snapshotSequencer = sequencer ?? RoomSnapshotSequencer.Shared;

    /// <summary>The public view of the room: no viewer, so games with hidden information return only what everyone may see.</summary>
    public Task<RoomSnapshot> GetSnapshotAsync(Guid roomId, CancellationToken ct = default) =>
        BuildSnapshotAsync(roomId, viewer: null, ct);

    /// <summary>The room as one specific player may see it.</summary>
    public Task<RoomSnapshot> GetSnapshotForAsync(Guid roomId, string viewer, CancellationToken ct = default) =>
        BuildSnapshotAsync(roomId, viewer, ct);

    private Task<RoomSnapshot> BuildSnapshotAsync(Guid roomId, string? viewer, CancellationToken ct) =>
        snapshotSequencer.RunAsync(roomId, sequence => BuildSnapshotAsync(roomId, viewer, sequence, ct), ct);

    private async Task<RoomSnapshot> BuildSnapshotAsync(Guid roomId, string? viewer, long sequence, CancellationToken ct)
    {
        var room = await RequireRoomAsync(roomId, ct);
        var session = await CurrentSessionAsync(roomId, ct);
        var players = await db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId).ToListAsync(ct);
        var engine = EngineFor(room.GameType);

        return new RoomSnapshot(
            room.Id,
            room.Slug,
            room.Title,
            room.HostPlayer,
            room.GameType,
            new SessionView(session.Id, session.Number, session.Status, session.StartedAt, session.EndedAt),
            players.Select(p => new PlayerView(p.Name, presence.IsOnline(roomId, p.Name), p.IsClaimed)).ToList(),
            viewer is null
                ? await engine.GetPayloadAsync(roomId, session.Id, ct)
                : await engine.GetPayloadForAsync(roomId, session.Id, viewer, ct),
            sequence);
    }

    public async Task<RoomSnapshot> PerformActionAsync(
        Guid roomId, string actor, string action, JsonElement? payload, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(roomId, ct);
        var isPlayer = await db.RoomPlayers.AnyAsync(p => p.RoomId == roomId && p.Name == actor, ct);
        if (!isPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only players in this room can act here.");

        var session = await CurrentSessionAsync(roomId, ct);
        if (session.Status != SessionStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "The session is not active.");

        var engine = EngineFor(room.GameType);
        await engine.HandleActionAsync(roomId, session.Id, actor, action, payload, ct);

        if (await engine.IsSessionCompleteAsync(session.Id, ct))
        {
            Complete(session);
            await db.SaveChangesAsync(ct);
        }

        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    public async Task<RoomSnapshot> StartSessionAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(roomId, ct);
        var session = await CurrentSessionForHostAsync(roomId, actor, ct);
        if (session.Status != SessionStatus.Waiting)
            throw new RoomRuleException(RuleViolation.Conflict, "Only a waiting session can be started.");

        Activate(session);
        await db.SaveChangesAsync(ct);
        await EngineFor(room.GameType).OnSessionStartedAsync(roomId, session.Id, ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    public async Task<RoomSnapshot> EndSessionAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var session = await CurrentSessionForHostAsync(roomId, actor, ct);
        if (session.Status != SessionStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "Only an active session can be ended.");

        Complete(session);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    public async Task<RoomSnapshot> StartNewSessionAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(roomId, ct);
        var session = await CurrentSessionForHostAsync(roomId, actor, ct);
        if (session.Status != SessionStatus.Completed)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current session before starting a new one.");

        var next = NewSession(roomId, session.Number + 1);
        db.GameSessions.Add(next);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RoomRuleException(RuleViolation.Conflict, "The room changed; please refresh.");
        }

        var engine = EngineFor(room.GameType);
        await engine.OnSessionCreatedAsync(roomId, next.Id, ct);
        Activate(next);
        await db.SaveChangesAsync(ct);
        await engine.OnSessionStartedAsync(roomId, next.Id, ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    private IGameEngine EngineFor(string gameType) =>
        engines.FirstOrDefault(e => e.GameType == gameType)
        ?? throw new InvalidOperationException($"No IGameEngine registered for game type '{gameType}'.");

    private GameSession NewSession(Guid roomId, int number) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        Number = number,
        Status = SessionStatus.Waiting,
        CreatedAt = clock.GetUtcNow(),
    };

    private void Activate(GameSession session)
    {
        session.Status = SessionStatus.Active;
        session.StartedAt = clock.GetUtcNow();
    }

    private void Complete(GameSession session)
    {
        session.Status = SessionStatus.Completed;
        session.EndedAt = clock.GetUtcNow();
    }

    private async Task<Room> RequireRoomAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct)
        ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");

    private async Task<GameSession> CurrentSessionAsync(Guid roomId, CancellationToken ct) =>
        await db.GameSessions.Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).FirstAsync(ct);

    private async Task<GameSession> CurrentSessionForHostAsync(Guid roomId, string actor, CancellationToken ct)
    {
        var room = await RequireRoomAsync(roomId, ct);
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control the session.");
        return await CurrentSessionAsync(roomId, ct);
    }
}
