namespace RandomRoom.Tests.EndToEnd;

/// <summary>Every game that has a strategy. A new game's strategy is added here and the suites pick it up.</summary>
public static class GameStrategies
{
    public const string WouldYouRather = "would-you-rather";
    public const string Trivia = "trivia";
    public const string Bingo = "bingo";

    /// <summary>The games the pull-request run plays: a small, fast subset of <see cref="All"/>.</summary>
    public static readonly string[] PullRequestSubset = [WouldYouRather, Trivia, Bingo];

    public static readonly string[] All = [WouldYouRather, Trivia, Bingo];

    public static TheoryData<string> PullRequestSubsetData() => Data(PullRequestSubset);

    public static TheoryData<string> AllData() => Data(All);

    /// <summary>A fresh strategy: they remember what their bots did, so one is never shared between games. Size is rounds or questions.</summary>
    public static IGameStrategy Create(string gameType, int size = 3) => gameType switch
    {
        WouldYouRather => new WouldYouRatherStrategy(size),
        Trivia => new TriviaStrategy(size),
        Bingo => new BingoStrategy(),
        _ => throw new ArgumentException($"No strategy for '{gameType}'.", nameof(gameType)),
    };

    private static TheoryData<string> Data(IEnumerable<string> games)
    {
        var data = new TheoryData<string>();
        foreach (var game in games) data.Add(game);
        return data;
    }
}
