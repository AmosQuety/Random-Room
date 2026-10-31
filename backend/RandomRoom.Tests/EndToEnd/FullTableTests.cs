namespace RandomRoom.Tests.EndToEnd;

/// <summary>Every game with a strategy, played by a full room of twelve over longer games. Nightly only.</summary>
[Trait("Category", "EndToEnd")]
[Trait("Suite", "Nightly")]
public sealed class FullTableTests(EndToEndFactory factory) : IClassFixture<EndToEndFactory>
{
    private const int MaxPlayers = 12;
    private const int LongGame = 8;

    public static TheoryData<string> Games => GameStrategies.AllData();

    [Theory]
    [MemberData(nameof(Games))]
    public async Task A_full_room_plays_a_long_game_without_anyone_seeing_a_secret(string game)
    {
        var strategy = GameStrategies.Create(game, size: LongGame);
        await using var table = await GameTable.OpenAsync(factory, strategy, MaxPlayers);

        await GamePlay.PlayToCompletionAsync(table, strategy);

        await GamePlay.AssertFinishedCleanlyAsync(table, strategy);
    }
}
