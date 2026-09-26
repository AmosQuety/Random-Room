using Npgsql;

namespace RandomRoom.Api.Data;

public static class DatabaseConnection
{
    public const string LocalDefault = "Host=localhost;Port=5433;Database=randomroom;Username=postgres;Password=postgres";

    /// <summary>
    /// Accepts either an Npgsql connection string or a postgres:// URL (the format Render provides)
    /// and returns an Npgsql connection string.
    /// </summary>
    public static string Resolve(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return LocalDefault;
        return IsPostgresUrl(configured) ? FromUrl(configured) : configured;
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
            // Render's external URL requires TLS; the internal one accepts it too.
            SslMode = SslMode.Prefer,
        };
        return builder.ConnectionString;
    }
}
