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
    public void Plain_connection_string_is_passed_through()
    {
        const string plain = "Host=h;Database=d;Username=u;Password=p";

        Assert.Equal(plain, DatabaseConnection.Resolve(plain));
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
