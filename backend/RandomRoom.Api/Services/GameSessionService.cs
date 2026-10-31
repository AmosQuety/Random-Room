using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Shared;

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
        var session = await LatestSessionAsync(roomId, ct);
        var players = await db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId).ToListAsync(ct);
        var engine = EngineFor(room.GameType);

        return new RoomSnapshot(
            room.Id,
            room.Slug,
            room.Title,
            room.HostPlayer,
            room.HostPlays,
            room.GameType,
            new SessionView(session.Id, session.Number, session.Status, session.StartedAt, session.EndedAt),
            players.Select(p => new PlayerView(p.Name, presence.IsOnline(roomId, p.Name), p.IsClaimed, session.HostPlays || p.Name != room.HostPlayer)).ToList(),
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
            throw new RoomRuleException(RuleViolation.Conflict, "This game is not active.");

        var engine = EngineFor(room.GameType);
        RequirePlayingOrRefereeAction(room, session, engine, actor, action);
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
            throw new RoomRuleException(RuleViolation.Conflict, "Only a game that has not started can be started.");
        await RequireEnoughParticipantsAsync(room, ct);

        Activate(session);
        await db.SaveChangesAsync(ct);
        await EngineFor(room.GameType).OnSessionStartedAsync(roomId, session.Id, ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    public async Task<RoomSnapshot> EndSessionAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var session = await CurrentSessionForHostAsync(roomId, actor, ct);
        if (session.Status != SessionStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "Only a game in progress can be ended.");

        Complete(session);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    public async Task<RoomSnapshot> StartNewSessionAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var (room, next) = await CreateNextSessionAsync(roomId, actor, ct);

        var engine = EngineFor(room.GameType);
        await engine.OnSessionCreatedAsync(roomId, next.Id, ct);
        Activate(next);
        await db.SaveChangesAsync(ct);
        await engine.OnSessionStartedAsync(roomId, next.Id, ct);
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    // Under the room lock, so SetHostPlaysAsync cannot change who plays between the range check and the new session.
    // Once the new session is committed it is the latest and no longer Completed, so the toggle is refused until it ends.
    private async Task<(Room Room, GameSession Next)> CreateNextSessionAsync(Guid roomId, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoomAsync(roomId, ct);
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control the game.");
        var latest = await LatestSessionAsync(roomId, ct);
        if (latest.Status != SessionStatus.Completed)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current game before starting a new one.");
        await RequireEnoughParticipantsAsync(room, ct);

        var next = NewSession(roomId, latest.Number + 1, latest.GameType, room.HostPlays);
        db.GameSessions.Add(next);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RoomRuleException(RuleViolation.Conflict, "The room changed; please refresh.");
        }
        await transaction.CommitAsync(ct);
        return (room, next);
    }

    /// <summary>
    /// Lets the host choose whether they play from the next game. Only between games (the current game is Completed),
    /// because a game builds its turns, teams and cards from the participants. Audited and range-checked.
    /// </summary>
    public async Task<RoomSnapshot> SetHostPlaysAsync(Guid roomId, string actor, bool plays, CancellationToken ct = default)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var room = await LockRoomAsync(roomId, ct);
            if (actor != room.HostPlayer)
                throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control the game.");
            var latest = await LatestSessionAsync(roomId, ct);
            if (latest.Status != SessionStatus.Completed)
                throw new RoomRuleException(RuleViolation.Conflict, "You can change this only between games. End the current game first.");

            var engine = EngineFor(room.GameType);
            // Asking for the current value is a no-op, even in a game that requires a referee: a Buzzer room made
            // through the API may already have HostPlays true.
            if (room.HostPlays != plays)
            {
                if (plays && engine.RequiresReferee)
                    throw new RoomRuleException(RuleViolation.InvalidInput, "In this game the host always judges, so the host cannot play.");

                var seats = await db.RoomPlayers.CountAsync(p => p.RoomId == roomId, ct);
                ParticipantRange.Require(engine, Participants.CountAmong(seats, plays), plays);
                await db.Rooms.Where(r => r.Id == roomId).ExecuteUpdateAsync(set => set.SetProperty(r => r.HostPlays, plays), ct);
                db.RoomAuditEvents.Add(HostPlaysAudit(roomId, actor, room.HostPlays, plays, latest.Number + 1));
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        return await GetSnapshotForAsync(roomId, actor, ct);
    }

    private RoomAuditEvent HostPlaysAudit(Guid roomId, string actor, bool from, bool to, int fromGame) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        Actor = actor,
        Action = RoomAuditEvent.HostPlaysChanged,
        // Fits the 32-character Target column: "referee->plays from game 12".
        Target = $"{Role(from)}->{Role(to)} from game {fromGame}",
        OccurredAt = clock.GetUtcNow(),
    };

    private static string Role(bool plays) => plays ? "plays" : "referee";

    // A row lock on the room, held until the caller's transaction ends: serializes the host-plays toggle with the
    // start of a new game. Untracked, so a long-lived context never returns a stale copy.
    private async Task<Room> LockRoomAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.FromSql($"SELECT * FROM \"Rooms\" WHERE \"Id\" = {roomId} FOR UPDATE").AsNoTracking().FirstOrDefaultAsync(ct)
        ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");

    // A referee runs the game but never plays it: anything the game has not listed as a referee action is refused.
    private static void RequirePlayingOrRefereeAction(Room room, GameSession session, IGameEngine engine, string actor, string action)
    {
        if (!session.HostPlays && actor == room.HostPlayer && !engine.RefereeActions.Contains(action))
            throw new RoomRuleException(RuleViolation.Forbidden, "You are refereeing this game, so you do not play.");
    }

    // Rooms where the host plays were range-checked at creation and behave as before; only a referee room needs the
    // check here, because its participant count differs from its seat count.
    private async Task RequireEnoughParticipantsAsync(Room room, CancellationToken ct)
    {
        if (room.HostPlays) return;

        // The room's flag, not the latest session's: this checks the game about to be played.
        var seats = await db.RoomPlayers.CountAsync(p => p.RoomId == room.Id, ct);
        ParticipantRange.Require(EngineFor(room.GameType), Participants.CountAmong(seats, room.HostPlays), room.HostPlays);
    }

    private IGameEngine EngineFor(string gameType) =>
        engines.FirstOrDefault(e => e.GameType == gameType)
        ?? throw new InvalidOperationException($"No IGameEngine registered for game type '{gameType}'.");

    private GameSession NewSession(Guid roomId, int number, string gameType, bool hostPlays) => new()
    {
        GameType = gameType,
        HostPlays = hostPlays,
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

    // Untracked, so a snapshot always reflects the database and never a copy this request loaded before another
    // request (such as the host ending the game) committed. The sequencer relies on a later build reading fresher state.
    private async Task<GameSession> LatestSessionAsync(Guid roomId, CancellationToken ct) =>
        await db.GameSessions.AsNoTracking().Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).FirstAsync(ct);

    private async Task<GameSession> CurrentSessionAsync(Guid roomId, CancellationToken ct) =>
        await db.GameSessions.Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).FirstAsync(ct);

    private async Task<GameSession> CurrentSessionForHostAsync(Guid roomId, string actor, CancellationToken ct)
    {
        var room = await RequireRoomAsync(roomId, ct);
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control the game.");
        return await CurrentSessionAsync(roomId, ct);
    }
}
