using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RandomRoom.Api.Data;
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

public sealed class RoomTestHarness : IDisposable
{
    private readonly TestDatabase database = new();

    public RoomDbContext Db { get; }
    public PresenceTracker Presence { get; } = new();
    public RoomService Room { get; }

    public RoomTestHarness(params int[] randomIndexes)
    {
        Db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        Db.Database.Migrate();

        var options = Options.Create(new RoomOptions { HostPlayer = "Amos" });
        Room = new RoomService(Db, new FakeRandomSource(randomIndexes), Presence, options, TimeProvider.System);
        Room.EnsureFirstRoundAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Db.Dispose();
        database.Dispose();
    }
}
