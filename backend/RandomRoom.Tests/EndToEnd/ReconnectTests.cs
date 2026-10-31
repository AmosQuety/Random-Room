namespace RandomRoom.Tests.EndToEnd;

/// <summary>A player whose connection drops mid-game comes back to the same room state, as the frontend's refetch does.</summary>
[Trait("Category", "EndToEnd")]
public sealed class ReconnectTests(EndToEndFactory factory) : IClassFixture<EndToEndFactory>
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    public static TheoryData<string> Games => GameStrategies.PullRequestSubsetData();

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Reconnecting_mid_game_restores_the_same_state(string game)
    {
        var strategy = GameStrategies.Create(game, size: 4);
        await using var table = await GameTable.OpenAsync(factory, strategy, playerCount: 4);
        var player = table.Bots[^1];

        await GamePlay.PlayToCompletionAsync(table, strategy, async step =>
        {
            if (step == 1) await AssertReconnectingChangesNothingAsync(player);
        });

        await GamePlay.AssertFinishedCleanlyAsync(table, strategy);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task A_player_who_missed_a_step_while_offline_catches_up_on_reconnecting(string game)
    {
        var strategy = GameStrategies.Create(game, size: 4);
        await using var table = await GameTable.OpenAsync(factory, strategy, playerCount: 4);
        var player = table.Bots[^1];

        await GamePlay.PlayToCompletionAsync(table, strategy, async step =>
        {
            if (step == 1) await player.DisconnectAsync();
            if (step == 2) await AssertCatchesUpAsync(table, player);
        });

        await GamePlay.AssertFinishedCleanlyAsync(table, strategy);
    }

    private static async Task AssertReconnectingChangesNothingAsync(Bot player)
    {
        await Task.Delay(Quiet);
        var before = GamePlay.Stable(player.Latest!);

        await player.DisconnectAsync();
        await player.ConnectAsync();

        Assert.Equal(before, GamePlay.Stable(player.Latest!));
    }

    private static async Task AssertCatchesUpAsync(GameTable table, Bot player)
    {
        Assert.NotEqual(GamePlay.StablePublicView(table.Host.Latest!), GamePlay.StablePublicView(player.Latest!));

        await player.ConnectAsync();
        await Task.Delay(Quiet);

        Assert.Equal(GamePlay.StablePublicView(table.Host.Latest!), GamePlay.StablePublicView(player.Latest!));
    }
}
