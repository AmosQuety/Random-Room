using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.Trivia;

namespace RandomRoom.Tests;

/// <summary>
/// The frontend keeps its own copy of some server facts (game keys, player ranges, trivia limits, starter-pack sizes).
/// This test writes down what the server says now and checks the committed fixture still matches, so the frontend's
/// comparison test (games/serverFacts.test.ts) is always made against the truth. After a deliberate change to a fact,
/// refresh the fixture with: UPDATE_SERVER_FACTS=1 dotnet test backend/RandomRoom.slnx --filter ServerFactsTests
/// </summary>
public class ServerFactsTests(ApiTests.Factory factory) : IClassFixture<ApiTests.Factory>
{
    private const string UpdateVariable = "UPDATE_SERVER_FACTS";
    private const string FixtureRelativePath = "frontend/src/games/__fixtures__/server-facts.json";

    private sealed record GameFact(string Key, int MinPlayers, int MinPlayersWithReferee, int MaxPlayers, bool RequiresReferee);

    private sealed record TriviaFacts(
        int MaxQuestions,
        int MaxQuestionLength,
        int MaxOptionsPerQuestion,
        int MaxOptionLength,
        int MaxCategoryLength,
        SortedDictionary<string, int> StarterPackSizes);

    private sealed record ServerFacts(List<GameFact> Games, TriviaFacts Trivia);

    private static readonly JsonSerializerOptions FixtureJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void Facts_ListEveryRegisteredGameOnce()
    {
        var keys = CurrentFacts().Games.Select(g => g.Key).ToList();

        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void CommittedFixture_MatchesWhatTheEnginesSayNow()
    {
        var path = FixturePath();
        var current = JsonSerializer.Serialize(CurrentFacts(), FixtureJson) + "\n";

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, current);
            return;
        }

        Assert.True(File.Exists(path), $"{FixtureRelativePath} is missing. Create it with {UpdateVariable}=1.");
        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.True(committed == current,
            $"{FixtureRelativePath} is out of date with the server. If the change is intended, refresh it with {UpdateVariable}=1 and commit it.\n" +
            $"Server says:\n{current}\nFixture has:\n{committed}");
    }

    private ServerFacts CurrentFacts()
    {
        using var scope = factory.Services.CreateScope();
        var games = scope.ServiceProvider.GetServices<IGameEngine>()
            .Select(e => new GameFact(e.GameType, e.MinPlayers, e.MinPlayersWithReferee, e.MaxPlayers, e.RequiresReferee))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var packSizes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (pack, bank) in TriviaEngine.StarterPacks)
            packSizes[pack] = ContentBank.Load<JsonElement>(bank).Count;

        var trivia = new TriviaFacts(
            TriviaEngine.MaxQuestions,
            TriviaEngine.MaxQuestionLength,
            TriviaEngine.MaxOptionsPerQuestion,
            TriviaEngine.MaxOptionLength,
            TriviaEngine.MaxCategoryLength,
            packSizes);

        return new ServerFacts(games, trivia);
    }

    private static string FixturePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "frontend", "package.json")))
                return Path.Combine(dir.FullName, FixtureRelativePath);

        throw new InvalidOperationException($"Could not find the repository root (a folder with frontend/package.json) above {AppContext.BaseDirectory}.");
    }
}
