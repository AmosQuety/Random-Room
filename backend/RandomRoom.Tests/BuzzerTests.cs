using System.Text.Json;
using RandomRoom.Api.Games.Buzzer;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class BuzzerTests
{
    private static readonly string[] Prompts = ["Name a fruit", "Name a color", "Name an animal"];

    private static GameHarness NewGame(object? setup = null) =>
        new(d => new BuzzerEngine(d.Store, d.Random), BuzzerEngine.Key, setup ?? new { prompts = Prompts, useBuiltIn = false, rounds = 2 });

    private static BuzzerPayload Payload(RoomSnapshot snapshot) => (BuzzerPayload)snapshot.GamePayload;

    private static async Task<GameHarness> OpenRoundAsync()
    {
        var game = NewGame();
        await game.StartAsync();
        await game.ActAsync(game.Host, "open");
        return game;
    }

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    [Fact]
    public async Task A_round_starts_waiting_with_a_prompt_and_the_buzzers_closed()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());

        Assert.Equal(BuzzerEngine.Waiting, view.Phase);
        Assert.Equal(1, view.Round);
        Assert.Equal(2, view.TotalRounds);
        Assert.NotNull(view.Prompt);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "buzz")));
    }

    [Fact]
    public async Task The_first_buzz_is_recorded_and_visible_to_everyone()
    {
        using var game = await OpenRoundAsync();

        var view = Payload(await game.ActAsync("Lydia", "buzz"));

        Assert.Equal("Lydia", view.Buzzed);
        Assert.Equal("Lydia", Payload(await game.SnapshotAsync("James")).Buzzed);
    }

    [Fact]
    public async Task A_second_buzz_is_refused_and_does_not_replace_the_first()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("James", "buzz")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "buzz")));
        Assert.Equal("Lydia", Payload(await game.SnapshotAsync("Amos")).Buzzed);
    }

    [Fact]
    public async Task Simultaneous_buzzes_have_exactly_one_winner_who_is_the_one_shown()
    {
        using var game = await OpenRoundAsync();
        var clients = Enumerable.Range(0, 3).Select(_ => game.Reopen()).ToArray();
        var buzzers = new[] { "Lydia", "James", "Jacob" };

        var results = await Task.WhenAll(buzzers.Select(async (p, i) =>
        {
            try { await clients[i].ActAsync(p, "buzz"); return p; }
            catch (RoomRuleException) { return null; }
        }));
        foreach (var c in clients) c.Dispose();

        var winner = Assert.Single(results.Where(r => r is not null));
        Assert.Equal(winner, Payload(await game.SnapshotAsync("Amos")).Buzzed);
    }

    [Fact]
    public async Task The_host_does_not_buzz_and_the_buzzers_need_to_be_open()
    {
        using var game = NewGame();
        await game.StartAsync();
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "buzz")));
        await game.ActAsync("Amos", "open");

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Amos", "buzz")));
    }

    [Theory]
    [InlineData("open")]
    [InlineData("correct")]
    [InlineData("wrong")]
    [InlineData("skip")]
    [InlineData("next")]
    public async Task Only_the_host_can_run_the_round(string action)
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("James", action)));
    }

    [Fact]
    public async Task Each_point_the_host_gives_is_listed_so_the_board_is_not_a_mystery()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        var view = Payload(await game.ActAsync("Amos", "correct"));

        var note = Assert.Single(view.HostScoring);
        Assert.Equal((1, "Lydia", 1), (note.Round, note.Player, note.Points));
        Assert.Contains("judged", note.Reason);
    }

    [Fact]
    public async Task A_correct_answer_scores_and_resolves_the_round()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        var view = Payload(await game.ActAsync("Amos", "correct"));

        Assert.Equal(BuzzerEngine.Resolved, view.Phase);
        Assert.Equal("Lydia", view.Winner);
        Assert.Equal(1, view.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "correct")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("James", "buzz")));
    }

    [Fact]
    public async Task Judging_needs_a_buzz()
    {
        using var game = await OpenRoundAsync();

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "correct")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "wrong")));
    }

    [Fact]
    public async Task A_wrong_answer_locks_that_player_out_and_reopens_the_buzzers()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        var view = Payload(await game.ActAsync("Amos", "wrong"));

        Assert.Equal(BuzzerEngine.Open, view.Phase);
        Assert.Null(view.Buzzed);
        Assert.Equal(["Lydia"], view.LockedOut);
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "buzz")));
        Assert.Equal("James", Payload(await game.ActAsync("James", "buzz")).Buzzed);
    }

    [Fact]
    public async Task When_everyone_has_missed_the_round_ends_without_a_winner()
    {
        using var game = await OpenRoundAsync();
        foreach (var player in new[] { "Lydia", "James", "Jacob" })
        {
            await game.ActAsync(player, "buzz");
            await game.ActAsync("Amos", "wrong");
        }

        var view = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(BuzzerEngine.Resolved, view.Phase);
        Assert.Null(view.Winner);
        Assert.All(view.Scoreboard, s => Assert.Equal(0, s.Score));
    }

    [Fact]
    public async Task Skipping_ends_the_round_and_next_clears_lockouts()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");
        await game.ActAsync("Amos", "wrong");
        Assert.Equal(BuzzerEngine.Resolved, Payload(await game.ActAsync("Amos", "skip")).Phase);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "skip")));

        var next = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(BuzzerEngine.Waiting, next.Phase);
        Assert.Equal(2, next.Round);
        Assert.Empty(next.LockedOut);
        await game.ActAsync("Amos", "open");
        Assert.Equal("Lydia", Payload(await game.ActAsync("Lydia", "buzz")).Buzzed);
    }

    [Fact]
    public async Task The_game_completes_after_the_last_round()
    {
        using var game = NewGame(new { prompts = new[] { "Only one" }, useBuiltIn = false, rounds = 1 });
        await game.StartAsync();
        await game.ActAsync("Amos", "skip");

        Assert.Equal(Phases.Complete, Payload(await game.ActAsync("Amos", "next")).Phase);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "next")));
    }

    [Fact]
    public async Task Built_in_prompts_are_used_when_asked_for_and_the_round_count_caps_them()
    {
        using var game = NewGame(new { useBuiltIn = true, rounds = 5 });
        var view = Payload(await game.StartAsync());

        Assert.Equal(5, view.TotalRounds);
        Assert.False(string.IsNullOrWhiteSpace(view.Prompt));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("blank")]
    [InlineData("long")]
    [InlineData("nontext")]
    [InlineData("rounds")]
    public void Invalid_setup_is_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "none" => new { prompts = Array.Empty<string>(), useBuiltIn = false },
            "blank" => new { prompts = new[] { " " } },
            "long" => new { prompts = new[] { new string('x', BuzzerEngine.MaxPromptLength + 1) } },
            "nontext" => new { prompts = new object[] { 4 } },
            _ => new { prompts = Prompts, rounds = 0 },
        };

        Assert.Throws<RoomRuleException>(() => NewGame(setup));
    }

    [Fact]
    public void The_game_needs_three_players()
    {
        Assert.Throws<RoomRuleException>(() =>
            new GameHarness(d => new BuzzerEngine(d.Store, d.Random), BuzzerEngine.Key, new { prompts = Prompts }, ["Amos", "Lydia"]));
    }

    [Fact]
    public async Task An_unknown_action_is_rejected()
    {
        using var game = await OpenRoundAsync();
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "cheat")));
    }

    [Fact]
    public async Task State_survives_a_restart_including_who_buzzed()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync("Jacob"));

        Assert.Equal("Lydia", view.Buzzed);
        Assert.Equal(BuzzerEngine.Open, view.Phase);
    }

    [Fact]
    public async Task The_public_snapshot_carries_no_more_than_a_player_snapshot()
    {
        using var game = await OpenRoundAsync();
        await game.ActAsync("Lydia", "buzz");

        Assert.Equal(
            JsonSerializer.Serialize((await game.PublicSnapshotAsync()).GamePayload),
            JsonSerializer.Serialize((await game.SnapshotAsync("James")).GamePayload));
    }
}
