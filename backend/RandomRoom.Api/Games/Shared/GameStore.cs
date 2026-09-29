using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Shared;

/// <summary>A session's state row with its game-private data deserialized. Mutate Data/Phase/Round, then SaveStateAsync.</summary>
public sealed class SessionState<TData>(GameSessionState row, TData data)
{
    public GameSessionState Row { get; } = row;
    public TData Data { get; set; } = data;

    public string Phase { get => Row.Phase; set => Row.Phase = value; }
    public int Round { get => Row.Round; set => Row.Round = value; }
    public DateTimeOffset? DeadlineAt { get => Row.DeadlineAt; set => Row.DeadlineAt = value; }
}

/// <summary>One recorded player entry with its value deserialized.</summary>
public sealed record Entry<TValue>(Guid Id, int Round, string Kind, string Player, int Seq, TValue Value, DateTimeOffset ServerTime, long Ordinal);

/// <summary>
/// Persistence helpers shared by the round-based games: a room-level setup document, a per-session state
/// row with an optimistic concurrency token, and append-only player entries with a unique index. Keeping live
/// state here (not in memory) is what lets a restart or a browser refresh land back on the same screen.
/// </summary>
public sealed class GameStore(RoomDbContext db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DateTimeOffset Now => clock.GetUtcNow();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, JsonOptions);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("Stored game data was empty.");

    // ---- room-level setup ----

    public void AddSetup<T>(Guid roomId, T setup) =>
        db.RoomGameSetups.Add(new RoomGameSetup { Id = Guid.NewGuid(), RoomId = roomId, Json = Serialize(setup) });

    public async Task<T> GetSetupAsync<T>(Guid roomId, CancellationToken ct)
    {
        var row = await db.RoomGameSetups.AsNoTracking().SingleAsync(s => s.RoomId == roomId, ct);
        return Deserialize<T>(row.Json);
    }

    // ---- per-session state ----

    public void AddState<T>(Guid sessionId, string phase, T data) =>
        db.GameSessionStates.Add(new GameSessionState
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Phase = phase,
            Round = 0,
            DataJson = Serialize(data),
        });

    public async Task<SessionState<T>> LoadStateAsync<T>(Guid sessionId, CancellationToken ct)
    {
        var row = await db.GameSessionStates.SingleAsync(s => s.SessionId == sessionId, ct);
        // Reload so a state row changed by an ExecuteUpdate (a claim) is never served from a stale tracked copy.
        await db.Entry(row).ReloadAsync(ct);
        return new SessionState<T>(row, Deserialize<T>(row.DataJson));
    }

    public async Task SaveStateAsync<T>(SessionState<T> state, CancellationToken ct)
    {
        state.Row.DataJson = Serialize(state.Data);
        state.Row.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(state.Row).ReloadAsync(ct);
            throw new RoomRuleException(RuleViolation.Conflict, "The game moved on while you were acting. Try again.");
        }
    }

    /// <summary>
    /// Atomically claims the current round for a player if nobody has yet (first writer wins). The check and the
    /// write are a single UPDATE, so concurrent claimants cannot both succeed.
    /// </summary>
    public async Task<bool> TryClaimAsync(Guid sessionId, int round, string player, CancellationToken ct)
    {
        var updated = await db.GameSessionStates
            .Where(s => s.SessionId == sessionId && s.Round == round && s.Claimant == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Claimant, player).SetProperty(s => s.Version, s => s.Version + 1), ct);
        return updated == 1;
    }

    // ---- entries ----

    public async Task AddEntryAsync<T>(Guid sessionId, int round, string kind, string player, T value, string duplicateMessage, CancellationToken ct, int seq = 0)
    {
        var entry = new GameEntry
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Round = round,
            Kind = kind,
            Player = player,
            Seq = seq,
            ValueJson = Serialize(value),
            ServerTime = clock.GetUtcNow(),
        };
        db.GameEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Detach so a later save on this context does not retry the rejected insert.
            db.Entry(entry).State = EntityState.Detached;
            throw new RoomRuleException(RuleViolation.Conflict, duplicateMessage);
        }
    }

    public async Task<IReadOnlyList<Entry<T>>> EntriesAsync<T>(Guid sessionId, string kind, int? round, CancellationToken ct)
    {
        var query = db.GameEntries.AsNoTracking().Where(e => e.SessionId == sessionId && e.Kind == kind);
        if (round is not null) query = query.Where(e => e.Round == round);
        var rows = await query.OrderBy(e => e.Ordinal).ToListAsync(ct);
        return rows.Select(e => new Entry<T>(e.Id, e.Round, e.Kind, e.Player, e.Seq, Deserialize<T>(e.ValueJson), e.ServerTime, e.Ordinal)).ToList();
    }

    public Task<int> CountEntriesAsync(Guid sessionId, string kind, int round, CancellationToken ct) =>
        db.GameEntries.CountAsync(e => e.SessionId == sessionId && e.Kind == kind && e.Round == round, ct);

    // ---- room helpers ----

    public async Task<IReadOnlyList<string>> PlayerNamesAsync(Guid roomId, CancellationToken ct) =>
        await db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Name)
            .Select(p => p.Name).ToListAsync(ct);

    public async Task<Guid> RoomIdOfAsync(Guid sessionId, CancellationToken ct) =>
        await db.GameSessions.AsNoTracking().Where(s => s.Id == sessionId).Select(s => s.RoomId).SingleAsync(ct);

    public async Task<string> HostOfAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => r.HostPlayer).SingleAsync(ct);

    // ---- transactions ----

    /// <summary>
    /// Runs one action on one session: in a transaction, under the session's row lock, with its state loaded.
    /// Games that keep their own state use this so answers and phase moves are applied one at a time.
    /// </summary>
    public Task InSessionAsync<TData>(Guid sessionId, Func<SessionState<TData>, Task> work, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            await LockSessionAsync(sessionId, ct);
            await work(await LoadStateAsync<TData>(sessionId, ct));
        }, ct);

    /// <summary>
    /// Takes a row lock on the session's state until the surrounding transaction ends, so actions on one session
    /// (an answer racing the reveal, two hosts pressing next) are applied one at a time. Call inside InTransactionAsync.
    /// </summary>
    public async Task LockSessionAsync(Guid sessionId, CancellationToken ct) =>
        _ = await db.GameSessionStates
            .FromSql($"SELECT * FROM \"GameSessionStates\" WHERE \"SessionId\" = {sessionId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>Runs a multi-step write atomically. Nested calls join the outer transaction.</summary>
    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await work();
        await tx.CommitAsync(ct);
    }
}
