using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RandomRoom.Api.Data;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public sealed class FakeRandomSource(params int[] indexes) : IRandomChoiceSource
{
    private readonly Queue<int> queue = new(indexes);

    public int PickIndex(int count) => queue.Count > 0 ? queue.Dequeue() : 0;
}

/// <summary>
/// Creates a throwaway Postgres database per test so unique indexes and transactions behave as in production.
/// Needs a server: docker run -d -p 5433:5432 -e POSTGRES_PASSWORD=postgres postgres:16
/// (override the address with the RANDOMROOM_TEST_SERVER environment variable).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("RANDOMROOM_TEST_SERVER")
        ?? "Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=postgres";

    private readonly string name = $"rr_test_{Guid.NewGuid():N}";

    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(ServerConnection) { Database = name }.ConnectionString;

    public TestDatabase() => Execute($"CREATE DATABASE {name}");

    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        Execute($"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
    }

    private static void Execute(string sql)
    {
        using var connection = new NpgsqlConnection(ServerConnection);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}

/// <summary>Binds GameSessionService calls to one fixed test room, so gameplay-rule tests don't repeat the room id.</summary>
public sealed class RoomFacade(GameSessionService service, Guid roomId)
{
    public Task<RoomSnapshot> GetSnapshotAsync() => service.GetSnapshotAsync(roomId);
    public Task<RoomSnapshot> TriggerRandomAsync(string player) => service.PerformActionAsync(roomId, player, "trigger", null);
    public Task<RoomSnapshot> StartRoundAsync(string actor) => service.StartSessionAsync(roomId, actor);
    public Task<RoomSnapshot> EndRoundAsync(string actor) => service.EndSessionAsync(roomId, actor);
    public Task<RoomSnapshot> StartNewRoundAsync(string actor) => service.StartNewSessionAsync(roomId, actor);
}

/// <summary>Serializes a plain object into the JsonElement CreateRoomRequest.Setup expects.</summary>
public static class TestSetup
{
    public static JsonElement Of(object setup) => JsonSerializer.SerializeToElement(setup);
}

public sealed class RoomTestHarness : IDisposable
{
    public static readonly string[] Players = ["Amos", "Lydia", "James", "Jacob"];
    public static readonly string[] Choices = ["Sarah", "Judith"];
    public const string HostPlayer = "Amos";

    private readonly TestDatabase database = new();

    public RoomDbContext Db { get; }
    public PresenceTracker Presence { get; } = new();
    public Guid RoomId { get; }
    public RoomFacade Room { get; }

    private readonly int[] randomIndexes;

    public RoomTestHarness(params int[] randomIndexes)
    {
        this.randomIndexes = randomIndexes;
        Db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        Db.Database.Migrate();

        IGameEngine[] engines =
        [
            new RandomPickerEngine(Db, new FakeRandomSource(randomIndexes), TimeProvider.System),
            new TriviaEngine(Db, TimeProvider.System),
        ];

        var admin = new RoomAdminService(Db, TimeProvider.System, engines);
        var created = admin.CreateRoomAsync(new CreateRoomRequest(
            "Test room", RandomPickerEngine.Key, TestSetup.Of(new { choices = Choices }), Players, HostPlayer))
            .GetAwaiter().GetResult();
        RoomId = Db.Rooms.Single(r => r.Slug == created.Slug).Id;

        var service = new GameSessionService(Db, Presence, TimeProvider.System, engines);
        Room = new RoomFacade(service, RoomId);
    }

    /// <summary>A second service on its own DbContext over the same database: what a concurrent HTTP request gets.</summary>
    public RequestScope NewRequestScope() => new(database.ConnectionString, Presence, RoomId, randomIndexes);

    public void Dispose()
    {
        Db.Dispose();
        database.Dispose();
    }
}

public sealed class RequestScope : IDisposable
{
    private readonly RoomDbContext db;

    public RoomFacade Room { get; }

    public RequestScope(string connectionString, PresenceTracker presence, Guid roomId, int[] randomIndexes)
    {
        db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(connectionString).Options);
        IGameEngine[] engines =
        [
            new RandomPickerEngine(db, new FakeRandomSource(randomIndexes), TimeProvider.System),
            new TriviaEngine(db, TimeProvider.System),
        ];
        Room = new RoomFacade(new GameSessionService(db, presence, TimeProvider.System, engines), roomId);
    }

    public void Dispose() => db.Dispose();
}
