using System.Net;
using System.Text.Json;
using RandomRoom.Api.Games.Bingo;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// The host calls an item, each player marks what is on their own card, and anyone with a line claims bingo.
/// The secrets are the other players' cards (and marks), and the order the items will be called in.
/// </summary>
public sealed class BingoStrategy : IGameStrategy
{
    private const int PoolSize = 30;
    private static readonly string[] InternalFields = ["cards", "marks", "order"];

    private string? winner;

    public string GameType => "bingo";

    public object Setup(int playerCount) => new
    {
        items = Enumerable.Range(1, PoolSize).Select(i => $"Item {i:00}"),
        useBuiltIn = false,
    };

    public async Task<bool> PlayStepAsync(GameTable table)
    {
        var called = CalledCount(table.Host.Latest!);
        await table.Host.ActAsync("call");
        await table.AllSeeAsync(s => CalledCount(s) == called + 1, "the next item to be called");

        await table.AllActAsync((bot, _) => MarkEverythingCalledAsync(bot));

        var claims = await ClaimWithEveryLineAsync(table);
        if (claims.Count == 0) return false;

        Assert.Single(claims, c => c.Status == HttpStatusCode.OK);
        Assert.All(claims.Where(c => c.Status != HttpStatusCode.OK), c => Assert.Equal(HttpStatusCode.Conflict, c.Status));
        winner = claims.Single(c => c.Status == HttpStatusCode.OK).Player;

        await table.AllSeeAsync(s => s.IsCompleted, "the game to complete");
        AssertWinningLineIsReal(table);
        return true;
    }

    private async Task MarkEverythingCalledAsync(Bot bot)
    {
        var view = bot.Latest!.Payload;
        var card = CardOf(view);
        if (card is null) return;

        var called = view.GetProperty("called").EnumerateArray().Select(c => c.GetString()!).ToHashSet();
        var marked = MarksOf(view);
        for (var cell = 0; cell < card.Length; cell++)
            if (!marked[cell] && called.Contains(card[cell]))
                await bot.ActAsync("mark", new { cell });
    }

    private async Task<List<(string Player, HttpStatusCode Status)>> ClaimWithEveryLineAsync(GameTable table)
    {
        var claimants = table.Bots.Where(b => BingoCard.WinningLine(MarksOf(b.Latest!.Payload)) is not null).ToList();
        var results = await Task.WhenAll(claimants.Select(async b => (b.Name, await b.TryActAsync("bingo"))));
        return [.. results];
    }

    private void AssertWinningLineIsReal(GameTable table)
    {
        var result = table.Host.Latest!.Payload;
        Assert.Equal(winner, result.GetProperty("winner").GetString());

        var line = result.GetProperty("winningLine").EnumerateArray().Select(c => c.GetInt32()).ToList();
        var winnerMarks = MarksOf(table.Bots.Single(b => b.Name == winner).Latest!.Payload);
        Assert.All(line, cell => Assert.True(winnerMarks[cell], $"cell {cell} of the winning line was never marked"));
    }

    public IReadOnlyList<string> FindLeaks(GameTable table)
    {
        // A player's card is the first one the server showed them in that game; every later card must be the same one.
        var cards = table.Bots
            .SelectMany(b => b.Received.Select(s => (Bot: b.Name, s.SessionNumber, Card: CardOf(s.Payload))))
            .Where(x => x.Card is not null)
            .GroupBy(x => (x.Bot, x.SessionNumber))
            .ToDictionary(g => g.Key, g => g.First().Card!);

        var leaks = new List<string>();
        foreach (var bot in table.Bots)
            foreach (var snapshot in bot.Received)
                leaks.AddRange(LeaksIn(bot, snapshot, cards));
        return leaks;
    }

    private static List<string> LeaksIn(Bot viewer, Snapshot snapshot, Dictionary<(string Bot, int Game), string[]> cards)
    {
        var leaks = new List<string>();
        var where = $"{viewer.Name}, game {snapshot.SessionNumber}, sequence {snapshot.Sequence}";

        var shown = CardOf(snapshot.Payload);
        if (shown is not null && cards.TryGetValue((viewer.Name, snapshot.SessionNumber), out var own) && !shown.SequenceEqual(own))
            leaks.Add($"{viewer.Name} was shown a card that changed from the one dealt to them ({where})");

        foreach (var array in JsonScan.StringArrays(snapshot.Payload))
            foreach (var ((owner, game), card) in cards.Where(c => c.Key.Bot != viewer.Name && c.Key.Game == snapshot.SessionNumber))
                if (array.SequenceEqual(card))
                    leaks.Add($"{owner}'s card was sent to {viewer.Name} ({where})");

        foreach (var (path, name, _) in JsonScan.Properties(snapshot.Payload))
            if (InternalFields.Contains(name))
                leaks.Add($"internal game state '{path}' was sent ({where})");

        return leaks;
    }

    private static int CalledCount(Snapshot s) => s.Payload.GetProperty("called").GetArrayLength();

    private static string[]? CardOf(JsonElement payload) =>
        payload.TryGetProperty("myCard", out var card) && card.ValueKind == JsonValueKind.Array
            ? card.EnumerateArray().Select(c => c.GetString()!).ToArray()
            : null;

    private static bool[] MarksOf(JsonElement payload) =>
        payload.TryGetProperty("myMarks", out var marks) && marks.ValueKind == JsonValueKind.Array
            ? marks.EnumerateArray().Select(m => m.GetBoolean()).ToArray()
            : new bool[BingoCard.Cells];
}
