using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.SpinWheel;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class SpinWheelTests
{
    private static readonly string[] Segments = ["Sing a song", "Do a dance", "Tell a joke"];

    private static GameHarness NewGame(object? setup = null, params int[] picks) =>
        new(d => new SpinWheelEngine(d.Store, d.Random), SpinWheelEngine.Key, setup ?? new { segments = Segments, useBuiltIn = false, spins = 2 }, randomSource: new ScriptedRandom(picks));

    private static WheelPayload Payload(RoomSnapshot snapshot) => (WheelPayload)snapshot.GamePayload;

    [Fact]
    public async Task The_wheel_is_fixed_at_start_and_the_first_player_spins_first()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());

        Assert.Equal(SpinWheelEngine.Ready, view.Phase);
        Assert.Equal(Segments, view.Segments);
        Assert.Equal("Amos", view.Spinner);
        Assert.Equal(2, view.TotalRounds);
    }

    [Fact]
    public async Task The_server_chooses_the_segment_and_reports_it_to_everyone()
    {
        using var game = NewGame(null, 2);
        await game.StartAsync();

        var spun = Payload(await game.ActAsync("Amos", "spin"));

        Assert.Equal(SpinWheelEngine.Spun, spun.Phase);
        Assert.Equal(new WheelSpin("Amos", 2, "Tell a joke"), spun.Last);
        Assert.Equal(2, Payload(await game.SnapshotAsync("Lydia")).Last!.Index);
    }

    [Fact]
    public async Task Only_the_player_whose_turn_it_is_can_spin()
    {
        using var game = NewGame();
        await game.StartAsync();

        var error = await GameHarness.RejectedAsync(() => game.ActAsync("Lydia", "spin"));

        Assert.Equal(RuleViolation.Forbidden, error.Violation);
    }

    [Fact]
    public async Task A_spin_cannot_be_repeated_to_get_a_better_result()
    {
        using var game = NewGame(null, 1, 2);
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");

        var error = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "spin"));

        Assert.Equal(RuleViolation.Conflict, error.Violation);
        Assert.Equal(1, Payload(await game.SnapshotAsync("Amos")).Last!.Index);
    }

    [Fact]
    public async Task Simultaneous_spins_land_exactly_one_result()
    {
        using var game = NewGame(null, 0, 1, 2);
        await game.StartAsync();
        var second = game.Reopen();

        var attempts = await Task.WhenAll(Attempt(game), Attempt(second));

        Assert.Single(attempts, ok => ok);
        second.Dispose();

        static async Task<bool> Attempt(GameHarness g)
        {
            try { await g.ActAsync("Amos", "spin"); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    [Fact]
    public async Task A_player_other_than_the_host_cannot_award_when_the_host_is_not_the_one_spinning()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");
        await game.ActAsync("Lydia", "award");
        await game.ActAsync("Amos", "next");
        Assert.Equal("Jacob", Payload(await game.SnapshotAsync("Amos")).Spinner);
        await game.ActAsync("Jacob", "spin");

        Assert.Equal("host", Payload(await game.SnapshotAsync("James")).AwardMode);
        Assert.Equal(RuleViolation.Forbidden, (await GameHarness.RejectedAsync(() => game.ActAsync("James", "award"))).Violation);
        Assert.Equal(RuleViolation.Forbidden, (await GameHarness.RejectedAsync(() => game.ActAsync("Jacob", "award"))).Violation);
        var awarded = Payload(await game.ActAsync("Amos", "award"));

        Assert.True(awarded.Awarded);
        Assert.Equal(1, awarded.Scoreboard.Single(s => s.Player == "Jacob").Score);
        Assert.Equal(RuleViolation.Conflict, (await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "award"))).Violation);
    }

    [Fact]
    public async Task When_the_host_is_the_one_spinning_another_player_awards_and_the_host_cannot_award_themselves()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");

        Assert.Equal("players", Payload(await game.SnapshotAsync("Lydia")).AwardMode);
        Assert.Equal(RuleViolation.Forbidden, (await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "award"))).Violation);
        Assert.Equal(0, Payload(await game.SnapshotAsync("Amos")).Scoreboard.Single(s => s.Player == "Amos").Score);

        var awarded = Payload(await game.ActAsync("Lydia", "award"));

        Assert.True(awarded.Awarded);
        Assert.Equal(1, awarded.Scoreboard.Single(s => s.Player == "Amos").Score);
        Assert.Equal(RuleViolation.Conflict, (await GameHarness.RejectedAsync(() => game.ActAsync("James", "award"))).Violation);
    }

    [Fact]
    public async Task A_point_the_host_gives_is_listed_for_everyone_but_one_another_player_gives_is_not()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");
        await game.ActAsync("Lydia", "award");
        Assert.Empty(Payload(await game.SnapshotAsync("James")).HostScoring);

        await game.ActAsync("Amos", "next");
        await game.ActAsync("Jacob", "spin");
        await game.ActAsync("Amos", "award");

        var note = Assert.Single(Payload(await game.SnapshotAsync("James")).HostScoring);
        Assert.Equal(new HostScoreNote(2, "Jacob", 1, "The host gave the point"), note);
    }

    [Fact]
    public async Task Moving_on_without_a_point_is_listed_too_so_a_host_cannot_quietly_withhold_one()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");

        await game.ActAsync("Amos", "next");

        var note = Assert.Single(Payload(await game.SnapshotAsync("Lydia")).HostScoring);
        Assert.Equal(("Amos", 0, 1), (note.Player, note.Points, note.Round));
        Assert.Contains("without giving a point", note.Reason);
    }

    [Fact]
    public async Task Award_and_next_are_refused_before_a_spin()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.Conflict, (await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "award"))).Violation);
        Assert.Equal(RuleViolation.Conflict, (await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "next"))).Violation);
    }

    [Fact]
    public async Task Turns_rotate_through_the_players_and_the_game_completes_after_the_last_spin()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");
        var second = Payload(await game.ActAsync("Amos", "next"));

        var order = second.Scoreboard.Select(r => r.Player).ToList();
        Assert.Equal(order[1], second.Spinner);
        Assert.Null(second.Last);
        Assert.Equal(2, second.Round);

        await game.ActAsync(order[1], "spin");
        var done = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Null(done.Spinner);
        await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "next"));
    }

    [Fact]
    public async Task Built_in_challenges_fill_a_short_wheel_and_never_duplicate_custom_ones()
    {
        using var game = NewGame(new { segments = new[] { "Sing a song" }, useBuiltIn = true, spins = 1 });
        var view = Payload(await game.StartAsync());

        Assert.Equal(SpinWheelEngine.FillTo, view.Segments.Count);
        Assert.Equal("Sing a song", view.Segments[0]);
        Assert.Equal(view.Segments.Count, view.Segments.Select(AnswerNormalizer.Normalize).Distinct().Count());
    }

    [Fact]
    public async Task With_no_custom_segments_the_built_in_wheel_is_used()
    {
        using var game = NewGame(new { });
        Assert.Equal(SpinWheelEngine.FillTo, Payload(await game.StartAsync()).Segments.Count);
    }

    [Theory]
    [InlineData("one")]
    [InlineData("dupes")]
    [InlineData("blank")]
    [InlineData("long")]
    [InlineData("many")]
    [InlineData("nontext")]
    [InlineData("spins")]
    public void Invalid_setup_is_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "one" => new { segments = new[] { "Only" }, useBuiltIn = false },
            "dupes" => new { segments = new[] { "Same", " same " } },
            "blank" => new { segments = new[] { "A", "  " } },
            "long" => new { segments = new[] { "A", new string('x', SpinWheelEngine.MaxSegmentLength + 1) } },
            "many" => new { segments = Enumerable.Range(0, SpinWheelEngine.MaxSegments + 1).Select(i => $"S{i}") },
            "nontext" => new { segments = new object[] { "A", 5 } },
            _ => new { segments = Segments, spins = 0 },
        };

        Assert.Throws<RoomRuleException>(() => NewGame(setup));
    }

    [Fact]
    public async Task An_unknown_action_is_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();

        Assert.Equal(RuleViolation.InvalidInput, (await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "peek"))).Violation);
    }

    [Fact]
    public async Task State_survives_a_restart()
    {
        using var game = NewGame(null, 1);
        await game.StartAsync();
        await game.ActAsync("Amos", "spin");

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync("Jacob"));

        Assert.Equal(SpinWheelEngine.Spun, view.Phase);
        Assert.Equal("Do a dance", view.Last!.Label);
    }

    [Fact]
    public async Task The_public_preview_is_only_the_rule()
    {
        using var game = NewGame();
        var engine = new SpinWheelEngine(new GameStore(game.Db, game.Clock), new ScriptedRandom());

        var json = JsonSerializer.Serialize(await engine.GetRoomPreviewAsync(game.RoomId, default));

        Assert.DoesNotContain("Sing a song", json);
    }
}
