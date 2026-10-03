using System.Data.Common;
using Npgsql;

namespace RandomRoom.Api.Data;

public static class DatabaseConnection
{
    public const string LocalDefault = "Host=localhost;Port=5433;Database=randomroom;Username=postgres;Password=postgres";

    /// <summary>
    /// Accepts either an Npgsql connection string or a postgres:// URL (the format Render provides)
    /// and returns an Npgsql connection string. A database on another machine must use TLS unless the
    /// connection says otherwise: Npgsql's default (Prefer) would quietly fall back to plain text if an attacker
    /// on the path blocked encryption, exposing the password and every room's data.
    /// </summary>
    public static string Resolve(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return LocalDefault;
        var connection = IsPostgresUrl(configured) ? FromUrl(configured) : configured;
        return RequireTlsForRemote(connection);
    }

    private static string RequireTlsForRemote(string connection)
    {
        if (ChoosesSslMode(connection) || DatabaseMigrationPolicy.IsLocalDatabase(connection)) return connection;

        return new NpgsqlConnectionStringBuilder(connection) { SslMode = SslMode.Require }.ConnectionString;
    }

    /// <summary>True when the string itself names an SSL mode (a typed builder would report its default for every key).</summary>
    private static bool ChoosesSslMode(string connection)
    {
        var raw = new DbConnectionStringBuilder { ConnectionString = connection };
        return raw.Keys.Cast<string>().Any(key => key.Replace(" ", "").Replace("_", "").Equals("sslmode", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPostgresUrl(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
        };
        // Honour an explicit ?sslmode=... (for example prefer, for a private network that does not offer TLS).
        if (SslModeFromQuery(uri) is { } sslMode) builder.SslMode = sslMode;
        return builder.ConnectionString;
    }

    private static SslMode? SslModeFromQuery(Uri uri)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && parts.Length == 2
                && Enum.TryParse<SslMode>(parts[1].Replace("-", ""), ignoreCase: true, out var mode))
                return mode;
        }
        return null;
    }
}
