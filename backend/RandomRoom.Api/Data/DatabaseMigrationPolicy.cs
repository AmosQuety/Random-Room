using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RandomRoom.Api.Data;

/// <summary>
/// Decides whether the app may change the database schema when it starts. Migrating is only automatic when the
/// operator said so (<c>Database__AutoMigrate=true</c>) or when the database is on this machine, so a local
/// <c>.env</c> that points at a shared or production database can never migrate it by accident.
/// </summary>
public static class DatabaseMigrationPolicy
{
    public const string SettingName = "Database:AutoMigrate";

    /// <param name="autoMigrate">The setting: true always migrates, false never does, null (not set) migrates only a local database.</param>
    public static bool ShouldMigrate(bool? autoMigrate, string connectionString) =>
        autoMigrate ?? IsLocalDatabase(connectionString);

    public static bool IsLocalDatabase(string connectionString)
    {
        var hosts = new NpgsqlConnectionStringBuilder(connectionString).Host?.Split(',') ?? [];
        return hosts.Length > 0 && hosts.All(IsLocalHost);
    }

    private static bool IsLocalHost(string host)
    {
        host = host.Trim();
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address))
            // A unix socket path, which is always this machine.
            || host.StartsWith('/');
    }

    /// <summary>Migrates when allowed; otherwise refuses to start against a database that is behind, with the way to fix it.</summary>
    public static async Task ApplyAsync(RoomDbContext db, bool? autoMigrate, string connectionString, ILogger logger, CancellationToken ct = default)
    {
        if (ShouldMigrate(autoMigrate, connectionString))
        {
            await db.Database.MigrateAsync(ct);
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database is missing {pending.Count} migration(s) and this app was not allowed to apply them "
                + $"(it is not a local database and Database__AutoMigrate is not true). "
                + "Set Database__AutoMigrate=true to let the app migrate it, or run 'dotnet ef database update' yourself.");
        }
        logger.LogInformation("Not migrating automatically (the database is not local and Database__AutoMigrate is not true); the schema is up to date");
    }
}
