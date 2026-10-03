using Npgsql;
using RandomRoom.Api;
using RandomRoom.Api.Data;

namespace RandomRoom.Tests;

public class DatabaseConnectionTests
{
    [Fact]
    public void Render_style_url_is_converted_to_an_npgsql_connection_string()
    {
        var result = DatabaseConnection.Resolve("postgres://user:p%40ss@db.example.com:5432/roomdb");

        Assert.Contains("Host=db.example.com", result);
        Assert.Contains("Database=roomdb", result);
        Assert.Contains("Username=user", result);
        Assert.Contains("Password=p@ss", result);
    }

    [Fact]
    public void Url_without_a_port_uses_the_postgres_default()
    {
        Assert.Contains("Port=5432", DatabaseConnection.Resolve("postgresql://u:p@host/db"));
    }

    [Fact]
    public void A_local_plain_connection_string_is_passed_through_untouched()
    {
        const string plain = "Host=localhost;Port=5433;Database=d;Username=u;Password=p";

        Assert.Equal(plain, DatabaseConnection.Resolve(plain));
    }

    private static string SslModeOf(string resolved) => new NpgsqlConnectionStringBuilder(resolved).SslMode.ToString();

    [Theory]
    [InlineData("postgres://u:p@db.example.com/d")]
    [InlineData("postgresql://u:p@dpg-abc123-a/d")]
    [InlineData("Host=db.example.com;Database=d;Username=u;Password=p")]
    [InlineData("Host=10.0.0.5;Database=d;Username=u;Password=p")]
    public void A_database_on_another_machine_requires_tls_unless_told_otherwise(string connection) =>
        Assert.Equal("Require", SslModeOf(DatabaseConnection.Resolve(connection)));

    [Theory]
    [InlineData("postgres://u:p@localhost:5433/d")]
    [InlineData("postgres://u:p@127.0.0.1/d")]
    [InlineData("Host=localhost;Database=d;Username=u;Password=p")]
    [InlineData("Host=::1;Database=d;Username=u;Password=p")]
    public void A_database_on_this_machine_keeps_the_default_so_local_development_is_unchanged(string connection) =>
        Assert.Equal("Prefer", SslModeOf(DatabaseConnection.Resolve(connection)));

    [Fact]
    public void The_default_local_database_is_unchanged()
    {
        Assert.Equal(DatabaseConnection.LocalDefault, DatabaseConnection.Resolve(null));
        Assert.Equal(DatabaseConnection.LocalDefault, DatabaseConnection.Resolve("  "));
    }

    [Theory]
    [InlineData("postgres://u:p@dpg-abc123-a/d?sslmode=prefer", "Prefer")]
    [InlineData("postgres://u:p@db.example.com/d?sslmode=verify-full", "VerifyFull")]
    [InlineData("postgres://u:p@db.example.com/d?SSLMODE=disable", "Disable")]
    [InlineData("Host=dpg-abc123-a;Database=d;Username=u;Password=p;SSL Mode=Prefer", "Prefer")]
    [InlineData("Host=db.example.com;Database=d;Username=u;Password=p;SslMode=VerifyCA", "VerifyCA")]
    public void An_explicit_choice_is_respected_even_for_a_remote_database(string connection, string expected) =>
        Assert.Equal(expected, SslModeOf(DatabaseConnection.Resolve(connection)));

    [Fact]
    public void An_unrecognised_sslmode_in_a_url_does_not_weaken_the_default()
    {
        Assert.Equal("Require", SslModeOf(DatabaseConnection.Resolve("postgres://u:p@db.example.com/d?sslmode=whatever")));
    }

    [Fact]
    public void Missing_setting_falls_back_to_local_development_database()
    {
        Assert.Equal(DatabaseConnection.LocalDefault, DatabaseConnection.Resolve(null));
    }
}

public class DotEnvTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"dotenv-{Guid.NewGuid():N}");

    public void Dispose() => File.Delete(path);

    [Fact]
    public void Loads_values_skipping_comments_blanks_and_quotes()
    {
        File.WriteAllText(path, "# comment\n\nDOTENV_TEST_A=one\nDOTENV_TEST_B=\"two words\"\nnot a pair\n");

        DotEnv.Load(path);

        Assert.Equal("one", Environment.GetEnvironmentVariable("DOTENV_TEST_A"));
        Assert.Equal("two words", Environment.GetEnvironmentVariable("DOTENV_TEST_B"));
    }

    [Fact]
    public void Real_environment_variables_win_over_the_file()
    {
        Environment.SetEnvironmentVariable("DOTENV_TEST_C", "from-host");
        File.WriteAllText(path, "DOTENV_TEST_C=from-file");

        DotEnv.Load(path);

        Assert.Equal("from-host", Environment.GetEnvironmentVariable("DOTENV_TEST_C"));
    }

    [Fact]
    public void Missing_file_is_ignored()
    {
        DotEnv.Load(path + "-missing");
    }
}
