using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using RandomRoom.Api.Data;

namespace RandomRoom.Tests;

public class DatabaseMigrationPolicyTests
{
    private const string Local = "Host=localhost;Port=5433;Database=randomroom;Username=u;Password=p";
    private const string Remote = "Host=db.example.com;Database=randomroom;Username=u;Password=p";

    [Theory]
    [InlineData("Host=localhost;Database=d")]
    [InlineData("Host=LOCALHOST;Database=d")]
    [InlineData("Host=127.0.0.1;Database=d")]
    [InlineData("Host=127.0.5.9;Database=d")]
    [InlineData("Host=::1;Database=d")]
    [InlineData("Host=/var/run/postgresql;Database=d")]
    public void Databases_on_this_machine_are_local(string connection) =>
        Assert.True(DatabaseMigrationPolicy.IsLocalDatabase(connection));

    [Theory]
    [InlineData("Host=db.example.com;Database=d")]
    [InlineData("Host=10.0.0.5;Database=d")]
    [InlineData("Host=dpg-abc123-a;Database=d")]
    [InlineData("Host=localhost.evil.com;Database=d")]
    [InlineData("Host=localhost,db.example.com;Database=d")]
    public void Everything_else_is_remote(string connection) =>
        Assert.False(DatabaseMigrationPolicy.IsLocalDatabase(connection));

    [Fact]
    public void Unset_migrates_a_local_database_and_leaves_a_remote_one_alone()
    {
        Assert.True(DatabaseMigrationPolicy.ShouldMigrate(null, Local));
        Assert.False(DatabaseMigrationPolicy.ShouldMigrate(null, Remote));
    }

    [Fact]
    public void An_explicit_setting_wins_either_way()
    {
        Assert.True(DatabaseMigrationPolicy.ShouldMigrate(true, Remote));
        Assert.False(DatabaseMigrationPolicy.ShouldMigrate(false, Local));
    }

    private static RoomDbContext Context(TestDatabase database) =>
        new(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);

    [Fact]
    public async Task When_not_allowed_an_empty_database_stops_the_app_and_says_how_to_fix_it()
    {
        using var database = new TestDatabase();
        await using var db = Context(database);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseMigrationPolicy.ApplyAsync(db, autoMigrate: false, database.ConnectionString, NullLogger.Instance));

        Assert.Contains("Database__AutoMigrate=true", ex.Message);
        Assert.Contains("dotnet ef database update", ex.Message);
        Assert.False(await db.Database.GetAppliedMigrationsAsync().ContinueWith(t => t.Result.Any()));
    }

    [Fact]
    public async Task When_allowed_it_applies_every_migration()
    {
        using var database = new TestDatabase();
        await using var db = Context(database);

        await DatabaseMigrationPolicy.ApplyAsync(db, autoMigrate: true, database.ConnectionString, NullLogger.Instance);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Unset_on_a_local_database_still_applies_migrations_so_development_just_works()
    {
        using var database = new TestDatabase();
        await using var db = Context(database);

        await DatabaseMigrationPolicy.ApplyAsync(db, autoMigrate: null, database.ConnectionString, NullLogger.Instance);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task When_not_allowed_an_up_to_date_database_starts_normally()
    {
        using var database = new TestDatabase();
        await using var db = Context(database);
        await db.Database.MigrateAsync();

        await DatabaseMigrationPolicy.ApplyAsync(db, autoMigrate: false, database.ConnectionString, NullLogger.Instance);
    }

    [Fact]
    public async Task When_not_allowed_a_database_that_is_one_migration_behind_stops_the_app_and_is_not_changed()
    {
        using var database = new TestDatabase();
        await using var db = Context(database);
        var all = db.Database.GetMigrations().ToList();
        await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(all[^2]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseMigrationPolicy.ApplyAsync(db, autoMigrate: false, database.ConnectionString, NullLogger.Instance));

        Assert.Contains("missing 1 migration", ex.Message);
        Assert.Single(await db.Database.GetPendingMigrationsAsync());
    }
}
