namespace RandomRoom.Tests.EndToEnd;

/// <summary>Whole games played by bots over HTTP and SignalR. The pull-request subset: three games, four players.</summary>
[Trait("Category", "EndToEnd")]
public sealed class GamePlayTests(EndToEndFactory factory) : IClassFixture<EndToEndFactory>
{
    public static TheoryData<string> Games => GameStrategies.PullRequestSubsetData();

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Bots_play_the_game_to_completion_without_anyone_seeing_a_secret(string game)
    {
        var strategy = GameStrategies.Create(game);
        await using var table = await GameTable.OpenAsync(factory, strategy, playerCount: 4);

        await GamePlay.PlayToCompletionAsync(table, strategy);

        await GamePlay.AssertFinishedCleanlyAsync(table, strategy);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task The_host_can_start_a_new_game_after_one_finishes(string game)
    {
        var strategy = GameStrategies.Create(game);
        await using var table = await GameTable.OpenAsync(factory, strategy, playerCount: 4);
        await GamePlay.PlayToCompletionAsync(table, strategy);
        await GamePlay.AssertFinishedCleanlyAsync(table, strategy);

        var second = GameStrategies.Create(game);
        await table.Host.StartNewGameAsync();
        await table.AllSeeAsync(s => s.Status == "Active" && s.SessionNumber == 2, "the second game to start");
        await GamePlay.PlayStepsAsync(table, second);

        await GamePlay.AssertFinishedCleanlyAsync(table, second);
    }
}
