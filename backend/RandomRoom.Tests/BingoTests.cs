using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Games.Bingo;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>A real, repeatable shuffle for games whose cards must differ from each other.</summary>
public sealed class SeededRandom(int seed = 7) : IRandomChoiceSource
{
    private readonly Random random = new(seed);

    public int PickIndex(int count) => random.Next(count);
}

public class BingoTests
{
    private static GameHarness NewGame(object? setup = null, string[]? players = null) =>
        new(d => new BingoEngine(d.Store, d.Random), BingoEngine.Key, setup ?? new { useBuiltIn = true }, players, new SeededRandom());

    private static BingoPayload Payload(RoomSnapshot snapshot) => (BingoPayload)snapshot.GamePayload;

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    /// <summary>Calls items until every cell in the given line of the player's card has been called, then marks them.</summary>
    private static async Task CompleteLineAsync(GameHarness game, string player, int[] line)
    {
        var card = Payload(await game.SnapshotAsync(player)).MyCard!;
        var needed = line.Where(c => c != BingoCard.FreeCell).Select(c => card[c]).ToHashSet();
        while (!needed.IsSubsetOf(Payload(await game.SnapshotAsync(player)).Called))
            await game.ActAsync(game.Host, "call");
        foreach (var cell in line.Where(c => c != BingoCard.FreeCell))
            await game.ActAsync(player, "mark", new { cell });
    }

    private static readonly int[] TopRow = [0, 1, 2, 3, 4];

    [Fact]
    public void Every_row_column_and_diagonal_is_a_winning_line_and_nothing_less_is()
    {
        var lines = new[]
        {
            new[] { 0, 1, 2, 3, 4 }, new[] { 5, 6, 7, 8, 9 }, new[] { 10, 11, 12, 13, 14 }, new[] { 15, 16, 17, 18, 19 }, new[] { 20, 21, 22, 23, 24 },
            new[] { 0, 5, 10, 15, 20 }, new[] { 1, 6, 11, 16, 21 }, new[] { 2, 7, 12, 17, 22 }, new[] { 3, 8, 13, 18, 23 }, new[] { 4, 9, 14, 19, 24 },
            new[] { 0, 6, 12, 18, 24 }, new[] { 4, 8, 12, 16, 20 },
        };
        foreach (var line in lines)
        {
            var marks = new bool[BingoCard.Cells];
            foreach (var cell in line) marks[cell] = true;
            Assert.Equal(line, BingoCard.WinningLine(marks));
            marks[line[0]] = false;
            Assert.Null(BingoCard.WinningLine(marks));
        }
    }

    [Fact]
    public async Task Each_player_gets_their_own_card_with_a_free_marked_centre()
    {
        using var game = NewGame();
        await game.StartAsync();

        var cards = new List<string>();
        foreach (var player in game.Players)
        {
            var view = Payload(await game.SnapshotAsync(player));
            Assert.Equal(BingoCard.Cells, view.MyCard!.Count);
            Assert.Equal("", view.MyCard[BingoCard.FreeCell]);
            Assert.True(view.MyMarks![BingoCard.FreeCell]);
            Assert.Equal(1, view.MyMarks.Count(m => m));
            Assert.Equal(BingoCard.ItemsPerCard, view.MyCard.Where(c => c != "").Distinct().Count());
            cards.Add(string.Join('|', view.MyCard));
        }
        Assert.Equal(cards.Count, cards.Distinct().Count());
    }

    [Fact]
    public async Task Cards_and_upcoming_calls_never_reach_anyone_else_or_the_public_view()
    {
        using var game = NewGame();
        await game.StartAsync();
        var lydiaCard = Payload(await game.SnapshotAsync("Lydia")).MyCard!;
        var order = (await game.Db.GameSessionStates.SingleAsync()).DataJson;
        var pool = JsonDocument.Parse(order).RootElement.GetProperty("order").EnumerateArray().Select(e => e.GetString()!).ToList();

        var amosView = JsonSerializer.Serialize((await game.SnapshotAsync("Amos")).GamePayload);
        var publicView = JsonSerializer.Serialize((await game.PublicSnapshotAsync()).GamePayload);

        Assert.Null(Payload(await game.PublicSnapshotAsync()).MyCard);
        foreach (var item in lydiaCard.Where(c => c != "")) Assert.DoesNotContain(item, publicView);
        Assert.NotEqual(JsonSerializer.Serialize(lydiaCard), JsonSerializer.Serialize(Payload(await game.SnapshotAsync("Amos")).MyCard));
        Assert.DoesNotContain("\"order\"", amosView, StringComparison.OrdinalIgnoreCase);
        Assert.All(pool, item => Assert.DoesNotContain($"\"{item}\"", publicView));
    }

    [Fact]
    public async Task Only_the_host_calls_and_each_call_reveals_exactly_one_more_item()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "call")));
        var first = Payload(await game.ActAsync("Amos", "call"));
        var second = Payload(await game.ActAsync("Amos", "call"));

        Assert.Single(first.Called);
        Assert.Equal(2, second.Called.Count);
        Assert.Equal(first.Called[0], second.Called[0]);
        Assert.Equal(2, Payload(await game.SnapshotAsync("Jacob")).Called.Count);
    }

    [Fact]
    public async Task Marking_an_item_that_has_not_been_called_is_refused()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "mark", new { cell = 0 })));
        Assert.False(Payload(await game.SnapshotAsync("Lydia")).MyMarks![0]);
    }

    [Fact]
    public async Task A_called_item_can_be_marked_and_marking_twice_changes_nothing()
    {
        using var game = NewGame();
        await game.StartAsync();
        var card = Payload(await game.SnapshotAsync("Lydia")).MyCard!;
        while (true)
        {
            var called = Payload(await game.ActAsync("Amos", "call")).Called;
            if (card.Contains(called[^1])) break;
        }
        var cell = Array.IndexOf(card.ToArray(), Payload(await game.SnapshotAsync("Lydia")).Called[^1]);

        await game.ActAsync("Lydia", "mark", new { cell });
        var again = Payload(await game.ActAsync("Lydia", "mark", new { cell }));

        Assert.True(again.MyMarks![cell]);
        Assert.Equal(2, again.MyMarks.Count(m => m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(1000)]
    public async Task Marking_a_cell_off_the_card_is_rejected(int cell)
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "mark", new { cell })));
    }

    [Fact]
    public async Task Malformed_mark_payloads_are_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "mark")));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "mark", new { cell = "1" })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "mark", new { cell = 1.5 })));
    }

    [Fact]
    public async Task A_claim_without_a_real_line_is_refused()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "bingo")));
        Assert.Equal(BingoEngine.Calling, Payload(await game.SnapshotAsync("Lydia")).Phase);
    }

    [Fact]
    public async Task A_marked_line_wins_the_game_for_that_player()
    {
        using var game = NewGame();
        await game.StartAsync();
        await CompleteLineAsync(game, "Lydia", TopRow);

        var won = Payload(await game.ActAsync("Lydia", "bingo"));

        Assert.Equal(Phases.Complete, won.Phase);
        Assert.Equal("Lydia", won.Winner);
        Assert.Equal(TopRow, won.WinningLine);
        Assert.Equal(1, won.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(0, won.Scoreboard.Single(s => s.Player == "James").Score);
    }

    [Fact]
    public async Task Once_someone_has_won_nothing_more_is_accepted()
    {
        using var game = NewGame();
        await game.StartAsync();
        await CompleteLineAsync(game, "Lydia", TopRow);
        await game.ActAsync("Lydia", "bingo");

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "bingo")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "call")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("James", "mark", new { cell = 0 })));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "end")));
    }

    [Fact]
    public async Task Two_players_claiming_at_once_produce_one_winner()
    {
        using var game = NewGame();
        await game.StartAsync();
        await CompleteLineAsync(game, "Lydia", TopRow);
        // Call on until James also has the top row called, so both can legitimately mark it.
        var jamesCard = Payload(await game.SnapshotAsync("James")).MyCard!;
        var jamesNeeded = TopRow.Select(c => jamesCard[c]).ToHashSet();
        while (!jamesNeeded.IsSubsetOf(Payload(await game.SnapshotAsync("James")).Called))
            await game.ActAsync("Amos", "call");
        foreach (var cell in TopRow) await game.ActAsync("James", "mark", new { cell });

        var second = game.Reopen();
        var results = await Task.WhenAll(Claim(game, "Lydia"), Claim(second, "James"));
        second.Dispose();

        Assert.Single(results.Where(r => r));
        var final = Payload(await game.SnapshotAsync("Amos"));
        Assert.Equal(1, final.Scoreboard.Sum(s => s.Score));
        Assert.NotNull(final.Winner);

        static async Task<bool> Claim(GameHarness g, string p)
        {
            try { await g.ActAsync(p, "bingo"); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    [Fact]
    public async Task Calling_stops_when_every_item_has_been_called_and_the_host_can_end_the_game()
    {
        using var game = NewGame(new { items = Enumerable.Range(0, BingoEngine.MinPool).Select(i => $"Item {i}"), useBuiltIn = false });
        await game.StartAsync();
        for (var i = 0; i < BingoEngine.MinPool; i++) await game.ActAsync("Amos", "call");

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "call")));
        Assert.Equal(Phases.Complete, Payload(await game.ActAsync("Amos", "end")).Phase);
        Assert.Null(Payload(await game.SnapshotAsync("Amos")).Winner);
    }

    [Fact]
    public async Task Only_the_host_can_end_the_game()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "end")));
    }

    [Fact]
    public async Task Custom_items_are_used_and_built_ins_are_optional()
    {
        var items = Enumerable.Range(0, 30).Select(i => $"Custom {i}").ToArray();
        using var game = NewGame(new { items, useBuiltIn = false });
        await game.StartAsync();

        var card = Payload(await game.SnapshotAsync("Lydia")).MyCard!;

        Assert.All(card.Where(c => c != ""), c => Assert.StartsWith("Custom", c));
        Assert.Equal(30, Payload(await game.SnapshotAsync("Lydia")).PoolSize);
    }

    [Theory]
    [InlineData("small")]
    [InlineData("dupes")]
    [InlineData("blank")]
    [InlineData("long")]
    [InlineData("many")]
    [InlineData("nontext")]
    public void Invalid_setup_is_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "small" => new { items = new[] { "a", "b" }, useBuiltIn = false },
            "dupes" => new { items = new[] { "Same", " same " } },
            "blank" => new { items = new[] { "a", " " } },
            "long" => new { items = new[] { new string('x', BingoEngine.MaxItemLength + 1) } },
            "many" => new { items = Enumerable.Range(0, BingoEngine.MaxItems + 1).Select(i => $"i{i}") },
            _ => new { items = new object[] { 1 } },
        };

        Assert.Throws<RoomRuleException>(() => NewGame(setup));
    }

    [Fact]
    public async Task An_unknown_action_is_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "peek")));
    }

    [Fact]
    public async Task Cards_marks_and_calls_survive_a_restart()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "call");
        var before = Payload(await game.SnapshotAsync("Lydia"));

        using var restarted = game.Reopen();
        var after = Payload(await restarted.SnapshotAsync("Lydia"));

        Assert.Equal(before.MyCard, after.MyCard);
        Assert.Equal(before.Called, after.Called);
    }
}
