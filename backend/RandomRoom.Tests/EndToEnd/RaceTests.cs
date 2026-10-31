using System.Net;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// Requests that reach the server at the same instant. Each is repeated many times because a race only shows up
/// on some interleavings. Nightly only: they are slow and add no new coverage of ordinary play.
/// </summary>
[Trait("Category", "EndToEnd")]
[Trait("Suite", "Nightly")]
public sealed class RaceTests(EndToEndFactory factory) : IClassFixture<EndToEndFactory>
{
    private const int Repetitions = 25;
    private const int JoinRepetitions = 6;

    [Theory]
    [InlineData(GameStrategies.WouldYouRather)]
    [InlineData(GameStrategies.Trivia)]
    public async Task Ending_the_game_as_a_player_answers_leaves_everyone_on_the_same_state(string game)
    {
        var strategy = GameStrategies.Create(game, size: 1);
        await using var table = await GameTable.OpenAsync(factory, strategy, playerCount: 4);

        for (var gameNumber = 1; gameNumber <= Repetitions; gameNumber++)
        {
            await StartGameAsync(table, gameNumber);
            var racer = await AllButOneAnswerAsync(table, game, gameNumber);

            var (ended, answered) = await RaceAsync(table, racer, game, gameNumber);

            AssertAtLeastOneSucceeded(ended, answered, gameNumber);
            await table.AllSeeAsync(s => s.IsCompleted && s.SessionNumber == gameNumber, $"game {gameNumber} to complete");
            await Task.Delay(100);
            GamePlay.AssertEveryoneEndedOnTheSameStatus(table);
        }
    }

    [Theory]
    [MemberData(nameof(JoinGames))]
    public async Task A_player_joining_as_the_host_starts_the_game_still_plays_the_whole_game(string game)
    {
        for (var attempt = 0; attempt < JoinRepetitions; attempt++)
        {
            var strategy = GameStrategies.Create(game);
            await using var table = await GameTable.OpenWithLatecomerAsync(factory, strategy, playerCount: 4);

            // Alternate which side moves first, by a few milliseconds, to meet different interleavings.
            var head = TimeSpan.FromMilliseconds(attempt % 4 * 5);
            await Task.WhenAll(
                AfterAsync(attempt % 2 == 0 ? head : TimeSpan.Zero, () => table.Host.StartGameAsync()),
                AfterAsync(attempt % 2 == 1 ? head : TimeSpan.Zero, () => table.SeatLatecomerAsync()));

            await table.AllSeeAsync(s => s.Status == "Active", "the game to start");
            await GamePlay.PlayStepsAsync(table, strategy);
            await GamePlay.AssertFinishedCleanlyAsync(table, strategy);
        }
    }

    public static TheoryData<string> JoinGames => GameStrategies.AllData();

    private static Task StartGameAsync(GameTable table, int gameNumber) =>
        gameNumber == 1 ? StartFirstGameAsync(table) : StartNextGameAsync(table, gameNumber);

    private static async Task StartFirstGameAsync(GameTable table)
    {
        await table.Host.StartGameAsync();
        await table.AllSeeAsync(s => s.Status == "Active" && s.SessionNumber == 1, "game 1 to start");
    }

    private static async Task StartNextGameAsync(GameTable table, int gameNumber)
    {
        await table.Host.StartNewGameAsync();
        await table.AllSeeAsync(s => s.Status == "Active" && s.SessionNumber == gameNumber, $"game {gameNumber} to start");
    }

    /// <summary>
    /// On odd repetitions every other player answers first, so the raced answer is the one that closes the round;
    /// on even ones it is the first answer in.
    /// </summary>
    private static async Task<Bot> AllButOneAnswerAsync(GameTable table, string game, int gameNumber)
    {
        var racer = table.Bots[^1];
        if (gameNumber % 2 == 0) return racer;

        foreach (var bot in table.Bots.Where(b => b != racer))
            await bot.ActAsync("answer", AnswerFor(game));
        return racer;
    }

    private static async Task<(HttpStatusCode Ended, HttpStatusCode Answered)> RaceAsync(GameTable table, Bot racer, string game, int gameNumber)
    {
        var go = new TaskCompletionSource();
        var end = Task.Run(async () =>
        {
            await go.Task;
            await Task.Delay(TimeSpan.FromMilliseconds(gameNumber % 4));
            return await table.Host.TryEndGameAsync();
        });
        var answer = Task.Run(async () =>
        {
            await go.Task;
            return await racer.TryActAsync("answer", AnswerFor(game));
        });

        go.SetResult();
        return (await end, await answer);
    }

    private static object AnswerFor(string game) =>
        game == GameStrategies.Trivia ? new { optionIndex = 0 } : new { choice = 0 };

    private static void AssertAtLeastOneSucceeded(HttpStatusCode ended, HttpStatusCode answered, int gameNumber)
    {
        // Losing the race is a clean refusal, never a server error.
        Assert.All([ended, answered], status => Assert.True(status is HttpStatusCode.OK or HttpStatusCode.Conflict, $"game {gameNumber}: got {(int)status}"));
        Assert.True(ended == HttpStatusCode.OK || answered == HttpStatusCode.OK, $"game {gameNumber}: neither request succeeded");
    }

    private static async Task AfterAsync(TimeSpan delay, Func<Task> work)
    {
        await Task.Delay(delay);
        await work();
    }
}
