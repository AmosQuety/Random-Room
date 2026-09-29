using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>A clock tests can move by hand, so deadlines are deterministic.</summary>
public sealed class ManualClock(DateTimeOffset? start = null) : TimeProvider
{
    private DateTimeOffset now = start ?? new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}

/// <summary>Random source that plays back scripted picks, then falls back to 0 (so shuffles keep order deterministic).</summary>
public sealed class ScriptedRandom(params int[] picks) : IRandomChoiceSource
{
    private readonly Queue<int> queue = new(picks);

    public int PickIndex(int count) => queue.Count > 0 ? queue.Dequeue() % count : 0;
}

/// <summary>What an engine factory receives, so tests wire engines the way Program.cs does.</summary>
public sealed record EngineDeps(RoomDbContext Db, GameStore Store, ManualClock Clock, IRandomChoiceSource Random);

/// <summary>
/// One throwaway-database room running one engine, with helpers that speak in players and actions.
/// Shared by every game's tests. Reopen() builds a fresh context and service over the same database,
/// which is how tests prove state survives a restart.
/// </summary>
public sealed class GameHarness : IDisposable
{
    public static readonly string[] DefaultPlayers = ["Amos", "Lydia", "James", "Jacob"];

    private readonly TestDatabase database = new();
    private readonly Func<EngineDeps, IGameEngine> factory;
    private readonly IRandomChoiceSource random;

    public ManualClock Clock { get; } = new();
    public RoomDbContext Db { get; private set; }
    public GameSessionService Service { get; private set; }
    public Guid RoomId { get; }
    public string[] Players { get; }
    public string Host => Players[0];

    public GameHarness(Func<EngineDeps, IGameEngine> engineFactory, string gameType, object setup, string[]? players = null, IRandomChoiceSource? randomSource = null)
    {
        factory = engineFactory;
        random = randomSource ?? new ScriptedRandom();
        Players = players ?? DefaultPlayers;

        Db = NewContext();
        try
        {
            Db.Database.Migrate();
            var (service, engine) = Wire(Db);
            Service = service;

            var admin = new RoomAdminService(Db, Clock, [engine]);
            var created = admin.CreateRoomAsync(new CreateRoomRequest("Test room", gameType, TestSetup.Of(setup), Players, Host))
                .GetAwaiter().GetResult();
            RoomId = Db.Rooms.Single(r => r.Slug == created.Slug).Id;
        }
        catch
        {
            // A rejected setup must not leave a throwaway database behind.
            Db.Dispose();
            database.Dispose();
            throw;
        }
    }

    private RoomDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);

    private (GameSessionService Service, IGameEngine Engine) Wire(RoomDbContext db)
    {
        var engine = factory(new EngineDeps(db, new GameStore(db, Clock), Clock, random));
        return (new GameSessionService(db, new PresenceTracker(), Clock, [engine]), engine);
    }

    /// <summary>A second, independent context and service over the same database (a "restarted server").</summary>
    public GameHarness Reopen() => new(this);

    private GameHarness(GameHarness original)
    {
        database = original.database;
        factory = original.factory;
        random = original.random;
        Clock = original.Clock;
        Players = original.Players;
        RoomId = original.RoomId;
        Db = NewContext();
        Service = Wire(Db).Service;
        ownsDatabase = false;
    }

    private readonly bool ownsDatabase = true;

    public Task<RoomSnapshot> StartAsync() => Service.StartSessionAsync(RoomId, Host);
    public Task<RoomSnapshot> EndAsync() => Service.EndSessionAsync(RoomId, Host);
    public Task<RoomSnapshot> NewRoundAsync() => Service.StartNewSessionAsync(RoomId, Host);

    public Task<RoomSnapshot> ActAsync(string player, string action, object? payload = null) =>
        Service.PerformActionAsync(RoomId, player, action, payload is null ? null : JsonSerializer.SerializeToElement(payload));

    public Task<RoomSnapshot> SnapshotAsync(string viewer) => Service.GetSnapshotForAsync(RoomId, viewer);

    public Task<RoomSnapshot> PublicSnapshotAsync() => Service.GetSnapshotAsync(RoomId);

    public async Task<T> PayloadAsync<T>(string viewer) => (T)(await SnapshotAsync(viewer)).GamePayload;

    public static async Task<RoomRuleException> RejectedAsync(Func<Task> action) =>
        await Assert.ThrowsAsync<RoomRuleException>(action);

    public void Dispose()
    {
        Db.Dispose();
        if (ownsDatabase) database.Dispose();
    }
}
