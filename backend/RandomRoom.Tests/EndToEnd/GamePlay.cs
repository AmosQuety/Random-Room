using System.Text.Json.Nodes;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>Drives a table through a whole game and holds the checks every run must pass.</summary>
public static class GamePlay
{
    private const int MaxSteps = 200;
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Starts the game and plays steps until the strategy says it is over. afterStep runs between steps (not after
    /// the last), which is where a test disconnects a player or injects a race.
    /// </summary>
    public static async Task PlayToCompletionAsync(GameTable table, IGameStrategy strategy, Func<int, Task>? afterStep = null)
    {
        await table.Host.StartGameAsync();
        await table.AllSeeAsync(s => s.Status == "Active", "the game to start");
        await PlayStepsAsync(table, strategy, afterStep);
    }

    /// <summary>Plays steps of a game that is already active, until it is over.</summary>
    public static async Task PlayStepsAsync(GameTable table, IGameStrategy strategy, Func<int, Task>? afterStep = null)
    {
        for (var step = 1; step <= MaxSteps; step++)
        {
            if (await strategy.PlayStepAsync(table)) return;
            if (afterStep is not null) await afterStep(step);
        }

        throw new TimeoutException($"{strategy.GameType} was still going after {MaxSteps} steps.");
    }

    /// <summary>Every run: the game is over, nobody is left on an older state, and no secret reached the wrong player.</summary>
    public static async Task AssertFinishedCleanlyAsync(GameTable table, IGameStrategy strategy)
    {
        await table.AllSeeAsync(s => s.IsCompleted, "the game to complete");
        await Task.Delay(SettleTime);

        AssertEveryoneEndedOnTheSameStatus(table);
        Assert.Empty(strategy.FindLeaks(table));
    }

    public static void AssertEveryoneEndedOnTheSameStatus(GameTable table)
    {
        var endings = table.Bots.Select(b => $"{b.Name}: {b.Latest!.Status} (game {b.Latest.SessionNumber})").ToList();
        Assert.True(table.Bots.All(b => b.Latest!.Status == "Completed"), "bots ended on different states: " + string.Join("; ", endings));
        Assert.Single(table.Bots.Select(b => b.Latest!.SessionNumber).Distinct());
    }

    /// <summary>
    /// What the room looks like, minus what legitimately changes between two reads of the same state: the sequence,
    /// the server clock and who is online.
    /// </summary>
    public static string Stable(Snapshot snapshot)
    {
        var node = JsonNode.Parse(snapshot.Raw.GetRawText())!;
        node.AsObject().Remove("sequence");
        foreach (var player in node["players"]!.AsArray()) player!.AsObject().Remove("online");
        RemoveServerClock(node);
        return node.ToJsonString();
    }

    /// <summary>
    /// <see cref="Stable"/> without the viewer's own fields (the ones named "my..."), so what two different players see
    /// of the same moment can be compared.
    /// </summary>
    public static string StablePublicView(Snapshot snapshot)
    {
        var node = JsonNode.Parse(Stable(snapshot))!;
        var payload = node["gamePayload"]!.AsObject();
        foreach (var name in payload.Select(p => p.Key).Where(k => k.StartsWith("my", StringComparison.Ordinal)).ToList())
            payload.Remove(name);
        return node.ToJsonString();
    }

    private static void RemoveServerClock(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("timer");
                foreach (var child in obj.Select(p => p.Value).ToList()) RemoveServerClock(child);
                break;
            case JsonArray array:
                foreach (var child in array) RemoveServerClock(child);
                break;
        }
    }
}
