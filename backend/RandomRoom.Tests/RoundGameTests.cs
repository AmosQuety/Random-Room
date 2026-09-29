using System.Text.Json;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>Builds harnesses for the round games the way Program.cs wires them.</summary>
internal static class RoundGames
{
    public static readonly object[] TwoPrompts =
    [
        new { a = "Pizza", b = "Tacos" },
        new { a = "Summer", b = "Winter" },
    ];

    public static GameHarness WouldYouRather(object? setup = null, string[]? players = null) =>
        Build(d => new RoundGameEngine<TwoWayPrompt>(new WouldYouRatherRules(), d.Store, d.Random, d.Clock), "would-you-rather",
            setup ?? new { prompts = TwoPrompts, rounds = 2 }, players);

    public static GameHarness MostLikelyTo(object? setup = null, string[]? players = null) =>
        Build(d => new RoundGameEngine<StatementPrompt>(new MostLikelyToRules(), d.Store, d.Random, d.Clock), "most-likely-to",
            setup ?? new { prompts = new[] { new { text = "Most likely to laugh first" }, new { text = "Most likely to nap" } }, rounds = 2 }, players);

    public static GameHarness NeverHaveIEver(object? setup = null, string[]? players = null) =>
        Build(d => new RoundGameEngine<StatementPrompt>(new NeverHaveIEverRules(), d.Store, d.Random, d.Clock), "never-have-i-ever",
            setup ?? new { prompts = new[] { new { text = "Never have I ever built a fort" } }, rounds = 1 }, players);

    public static GameHarness Survey(object? setup = null, string[]? players = null) =>
        Build(d => new RoundGameEngine<SurveyPrompt>(new SurveyShowdownRules(), d.Store, d.Random, d.Clock), "survey-showdown",
            setup ?? new { prompts = new[] { SurveyPromptJson() }, rounds = 1 }, players);

    public static object SurveyPromptJson() => new
    {
        text = "Name a pet",
        answers = new object[]
        {
            new { text = "Dog", points = 40, aliases = new[] { "Puppy" } },
            new { text = "Cat", points = 30 },
            new { text = "Fish", points = 10 },
        },
    };

    private static GameHarness Build(Func<EngineDeps, IGameEngine> factory, string key, object setup, string[]? players) =>
        new(factory, key, setup, players);

    public static async Task StartWithOrderAsync(GameHarness game) => await game.StartAsync();
}

public class RoundGameFlowTests
{
    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    [Fact]
    public async Task A_full_session_plays_through_rounds_reveals_and_completes()
    {
        using var game = RoundGames.WouldYouRather();
        var started = Payload(await game.StartAsync());
        Assert.Equal(Phases.Collecting, started.Phase);
        Assert.Equal(1, started.Round);
        Assert.Equal(2, started.TotalRounds);

        await game.ActAsync("Amos", "answer", new { choice = 0 });
        await game.ActAsync("Lydia", "answer", new { choice = 0 });
        await game.ActAsync("James", "answer", new { choice = 1 });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { choice = 0 }));

        Assert.Equal(Phases.Revealed, revealed.Phase);
        Assert.NotNull(revealed.Result);
        Assert.Equal(3, revealed.Result!.Value.GetProperty("counts")[0].GetInt32());
        Assert.Equal(1, revealed.Result!.Value.GetProperty("counts")[1].GetInt32());
        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Amos").Score);
        Assert.Equal(0, revealed.Scoreboard.Single(s => s.Player == "James").Score);

        var second = Payload(await game.ActAsync("Amos", "next"));
        Assert.Equal(2, second.Round);
        Assert.Equal(Phases.Collecting, second.Phase);
        Assert.Null(second.Result);

        await game.ActAsync("Amos", "answer", new { choice = 1 });
        await game.ActAsync("Lydia", "answer", new { choice = 1 });
        await game.ActAsync("James", "answer", new { choice = 1 });
        await game.ActAsync("Jacob", "answer", new { choice = 1 });
        var done = await game.ActAsync("Amos", "next");

        Assert.Equal(SessionStatus.Completed, done.Session.Status);
        Assert.Equal(2, Payload(done).Scoreboard.Single(s => s.Player == "Amos").Score);
    }

    [Fact]
    public async Task Answers_stay_private_until_the_reveal()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 1 });

        var lydia = Payload(await game.SnapshotAsync("Lydia"));
        var amos = Payload(await game.SnapshotAsync("Amos"));
        var publicView = Payload(await game.PublicSnapshotAsync());

        Assert.True(lydia.Answered["Amos"]);
        Assert.Null(lydia.MyAnswer);
        Assert.Null(lydia.Result);
        Assert.Equal(1, amos.MyAnswer!.Value.GetProperty("choice").GetInt32());
        Assert.Null(publicView.MyAnswer);
        Assert.DoesNotContain("voters", JsonSerializer.Serialize(lydia, lydia.GetType()));
    }

    [Fact]
    public async Task A_tie_scores_nobody()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 0 });
        await game.ActAsync("Lydia", "answer", new { choice = 0 });
        await game.ActAsync("James", "answer", new { choice = 1 });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { choice = 1 }));

        Assert.All(revealed.Scoreboard, s => Assert.Equal(0, s.Score));
        Assert.Equal(JsonValueKind.Null, revealed.Result!.Value.GetProperty("majority").ValueKind);
    }

    [Fact]
    public async Task The_host_can_reveal_early_and_only_once()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 0 });

        var revealed = Payload(await game.ActAsync("Amos", "reveal"));

        Assert.Equal(Phases.Revealed, revealed.Phase);
        var again = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "reveal"));
        Assert.Equal(RuleViolation.Conflict, again.Violation);
    }

    [Fact]
    public async Task Only_the_host_can_reveal_or_move_on()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();

        var reveal = await GameHarness.RejectedAsync(() => game.ActAsync("Lydia", "reveal"));
        await game.ActAsync("Amos", "reveal");
        var next = await GameHarness.RejectedAsync(() => game.ActAsync("Lydia", "next"));

        Assert.Equal(RuleViolation.Forbidden, reveal.Violation);
        Assert.Equal(RuleViolation.Forbidden, next.Violation);
    }

    [Fact]
    public async Task Actions_in_the_wrong_phase_are_rejected()
    {
        using var game = RoundGames.WouldYouRather();
        var beforeStart = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { choice = 0 }));
        await game.StartAsync();

        var nextTooSoon = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "next"));
        await game.ActAsync("Amos", "reveal");
        var lateAnswer = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { choice = 0 }));

        Assert.Equal(RuleViolation.Conflict, beforeStart.Violation);
        Assert.Equal(RuleViolation.Conflict, nextTooSoon.Violation);
        Assert.Equal(RuleViolation.Conflict, lateAnswer.Violation);
    }

    [Fact]
    public async Task Someone_outside_the_room_cannot_act()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Mallory", "answer", new { choice = 0 }));

        Assert.Equal(RuleViolation.Forbidden, ex.Violation);
    }

    [Fact]
    public async Task A_second_answer_from_the_same_player_is_rejected_and_not_counted()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 0 });

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { choice = 1 }));
        var view = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
        Assert.Equal(0, view.MyAnswer!.Value.GetProperty("choice").GetInt32());
        Assert.Single(view.Answered, a => a.Value);
    }

    [Theory]
    [InlineData("{\"choice\":2}")]
    [InlineData("{\"choice\":-1}")]
    [InlineData("{\"choice\":\"0\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task Malformed_answers_are_invalid_input(string json)
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task An_unknown_action_and_a_missing_answer_are_invalid_input()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();

        var unknown = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "cheat"));
        var empty = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer"));

        Assert.Equal(RuleViolation.InvalidInput, unknown.Violation);
        Assert.Equal(RuleViolation.InvalidInput, empty.Violation);
    }

    [Fact]
    public async Task State_survives_a_restart_mid_round()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 1 });

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync("Amos"));

        Assert.Equal(Phases.Collecting, view.Phase);
        Assert.Equal(1, view.Round);
        Assert.True(view.Answered["Amos"]);
        Assert.Equal(1, view.MyAnswer!.Value.GetProperty("choice").GetInt32());

        await restarted.ActAsync("Lydia", "answer", new { choice = 1 });
    }

    [Fact]
    public async Task Simultaneous_final_answers_reveal_exactly_once_and_count_everyone()
    {
        using var game = RoundGames.WouldYouRather();
        await game.StartAsync();
        var contexts = game.Players.Select(_ => game.Reopen()).ToList();

        try
        {
            await Task.WhenAll(game.Players.Select((p, i) => contexts[i].ActAsync(p, "answer", new { choice = 0 })));
            var view = Payload(await game.SnapshotAsync("Amos"));

            Assert.Equal(Phases.Revealed, view.Phase);
            Assert.Equal(4, view.Result!.Value.GetProperty("counts")[0].GetInt32());
            Assert.All(view.Scoreboard, s => Assert.Equal(1, s.Score));
        }
        finally
        {
            contexts.ForEach(c => c.Dispose());
        }
    }
}

public class RoundGameTimerTests
{
    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    private static GameHarness Timed() =>
        RoundGames.WouldYouRather(new { prompts = RoundGames.TwoPrompts, rounds = 2, timeLimitSeconds = 30 });

    [Fact]
    public async Task The_round_gets_a_server_deadline()
    {
        using var game = Timed();

        var view = Payload(await game.StartAsync());

        Assert.Equal(game.Clock.GetUtcNow().AddSeconds(30), view.Timer.DeadlineAt);
        Assert.Equal(30, view.TimeLimitSeconds);
    }

    [Fact]
    public async Task An_early_tick_changes_nothing()
    {
        using var game = Timed();
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromSeconds(29));

        var view = Payload(await game.ActAsync("Lydia", "tick"));

        Assert.Equal(Phases.Collecting, view.Phase);
    }

    [Fact]
    public async Task A_tick_after_the_deadline_reveals_with_whoever_answered()
    {
        using var game = Timed();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { choice = 0 });
        game.Clock.Advance(TimeSpan.FromSeconds(31));

        var view = Payload(await game.ActAsync("Lydia", "tick"));

        Assert.Equal(Phases.Revealed, view.Phase);
        Assert.Equal(1, view.Result!.Value.GetProperty("counts")[0].GetInt32());
    }

    [Fact]
    public async Task Answers_after_the_deadline_are_rejected()
    {
        using var game = Timed();
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromSeconds(31));

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { choice = 0 }));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public async Task Each_new_round_restarts_the_clock()
    {
        using var game = Timed();
        await game.StartAsync();
        await game.ActAsync("Amos", "reveal");
        game.Clock.Advance(TimeSpan.FromMinutes(5));

        var view = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(game.Clock.GetUtcNow().AddSeconds(30), view.Timer.DeadlineAt);
    }
}

public class RoundGameSetupTests
{
    private static RoomRuleException SetupRejected(object setup, string[]? players = null) =>
        Assert.Throws<RoomRuleException>(() =>
        {
            using var game = RoundGames.WouldYouRather(setup, players);
        });

    [Fact]
    public void A_setup_with_no_prompts_is_rejected()
    {
        var ex = SetupRejected(new { prompts = Array.Empty<object>() });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Rounds_must_fit_the_prompt_list(int rounds)
    {
        var ex = SetupRejected(new { prompts = RoundGames.TwoPrompts, rounds });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public void The_time_limit_must_be_in_range(int seconds)
    {
        var ex = SetupRejected(new { prompts = RoundGames.TwoPrompts, timeLimitSeconds = seconds });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void An_oversized_option_is_rejected()
    {
        var ex = SetupRejected(new { prompts = new[] { new { a = new string('x', 101), b = "Tacos" } } });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void A_missing_option_is_rejected()
    {
        var ex = SetupRejected(new { prompts = new[] { new { a = "Pizza", b = " " } } });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void Too_many_custom_prompts_are_rejected()
    {
        var ex = SetupRejected(new { prompts = Enumerable.Range(0, 101).Select(i => new { a = $"A{i}", b = $"B{i}" }).ToArray() });
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void Too_many_players_for_the_game_is_rejected()
    {
        var thirteen = Enumerable.Range(1, 13).Select(i => $"P{i}").ToArray();

        var ex = SetupRejected(new { prompts = RoundGames.TwoPrompts }, thirteen);

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void Built_in_prompts_can_be_used_alone_or_added_to()
    {
        using var builtIn = RoundGames.WouldYouRather(new { useBuiltIn = true, rounds = 5 });
        using var mixed = RoundGames.WouldYouRather(new { useBuiltIn = true, prompts = RoundGames.TwoPrompts, rounds = 32 });

        Assert.NotNull(builtIn);
        Assert.NotNull(mixed);
    }

    [Fact]
    public void The_public_preview_is_only_a_harmless_teaser()
    {
        IGameEngine? engine = null;
        using var game = new GameHarness(
            d => engine = new RoundGameEngine<TwoWayPrompt>(new WouldYouRatherRules(), d.Store, d.Random, d.Clock),
            "would-you-rather", new { prompts = RoundGames.TwoPrompts, rounds = 2 });

        var preview = engine!.GetRoomPreviewAsync(game.RoomId, CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(preview, preview.GetType(), JsonSerializerOptions.Web);

        Assert.DoesNotContain("Pizza", json);
        Assert.Contains("promptCount", json);
    }
}
