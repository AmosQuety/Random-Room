using System.Collections.Concurrent;
using System.Text.Json;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// Everyone answers privately, the round reveals, the host moves on. The secret is a player's pick: it must
/// reach only that player until the round is revealed.
/// </summary>
public sealed class WouldYouRatherStrategy(int rounds = 3) : IGameStrategy
{
    private readonly ConcurrentDictionary<(string Player, int Round), int> picks = new();

    public string GameType => "would-you-rather";

    public object Setup(int playerCount) => new
    {
        prompts = Enumerable.Range(1, rounds).Select(i => new { a = $"Option A{i}", b = $"Option B{i}" }),
        rounds,
    };

    public async Task<bool> PlayStepAsync(GameTable table)
    {
        var open = await table.Host.WaitForAsync(s => PhaseOf(s) == "collecting", "a round to open");
        var round = open.Payload.GetProperty("round").GetInt32();
        await table.AllSeeAsync(s => PhaseOf(s) == "collecting" && RoundOf(s) == round, $"round {round} to open");

        await table.AllActAsync((bot, index) =>
        {
            var choice = (index + round) % 2;
            picks[(bot.Name, round)] = choice;
            return bot.ActAsync("answer", new { choice });
        });

        var revealed = await table.Host.WaitForAsync(s => PhaseOf(s) == "revealed" && RoundOf(s) == round, $"round {round} to reveal");
        AssertTallyMatchesPicks(revealed, round);

        var isLastRound = round == revealed.Payload.GetProperty("totalRounds").GetInt32();
        await table.Host.ActAsync("next");
        if (isLastRound)
        {
            await table.AllSeeAsync(s => s.IsCompleted, "the game to complete");
            return true;
        }

        await table.AllSeeAsync(s => PhaseOf(s) == "collecting" && RoundOf(s) == round + 1, $"round {round + 1} to open");
        return false;
    }

    private void AssertTallyMatchesPicks(Snapshot revealed, int round)
    {
        var counts = revealed.Payload.GetProperty("result").GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ToArray();
        var expected = new[] { 0, 1 }.Select(side => picks.Count(p => p.Key.Round == round && p.Value == side)).ToArray();
        Assert.Equal(expected, counts);
    }

    public IReadOnlyList<string> FindLeaks(GameTable table)
    {
        var leaks = new List<string>();
        foreach (var bot in table.Bots)
            foreach (var snapshot in bot.Received)
                leaks.AddRange(LeaksIn(bot.Name, snapshot));
        return leaks;
    }

    private List<string> LeaksIn(string viewer, Snapshot snapshot)
    {
        var leaks = new List<string>();
        var payload = snapshot.Payload;
        var round = RoundOf(snapshot);

        if (PhaseOf(snapshot) == "collecting" && payload.TryGetProperty("result", out var result) && result.ValueKind != JsonValueKind.Null)
            leaks.Add($"{viewer} was sent round {round}'s result while answers were still open (sequence {snapshot.Sequence})");

        if (payload.TryGetProperty("myAnswer", out var mine) && mine.ValueKind == JsonValueKind.Object)
        {
            var shown = mine.GetProperty("choice").GetInt32();
            if (!picks.TryGetValue((viewer, round), out var own) || own != shown)
                leaks.Add($"{viewer} was shown answer {shown} for round {round}, which is not their own pick (sequence {snapshot.Sequence})");
        }

        return leaks;
    }

    private static string PhaseOf(Snapshot s) => s.Payload.GetProperty("phase").GetString()!;

    private static int RoundOf(Snapshot s) => s.Payload.GetProperty("round").GetInt32();
}
