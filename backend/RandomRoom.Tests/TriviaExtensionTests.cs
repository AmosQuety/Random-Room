using System.Text.Json;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class TriviaExtensionTests
{
    private static readonly object[] Two =
    [
        new { text = "2 + 2?", options = new[] { "3", "4" }, correctIndex = 1, category = "Maths" },
        new { text = "Capital of France?", options = new[] { "Paris", "Rome" }, correctIndex = 0 },
    ];

    private static GameHarness Trivia(object setup, string[]? players = null) =>
        new(d => new TriviaEngine(d.Db, d.Clock, d.Random), TriviaEngine.Key, setup, players);

    private static TriviaPayload Payload(RoomSnapshot snapshot) => (TriviaPayload)snapshot.GamePayload;

    private static RoomRuleException SetupRejected(object setup) =>
        Assert.Throws<RoomRuleException>(() => { using var g = Trivia(setup); });

    // ---- categories ----

    [Fact]
    public async Task A_question_can_carry_a_category()
    {
        using var game = Trivia(new { questions = Two });

        var view = Payload(await game.StartAsync());

        Assert.Equal("Maths", view.CurrentQuestion!.Category);
    }

    [Fact]
    public async Task A_question_without_a_category_has_none()
    {
        using var game = Trivia(new { questions = Two });
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { optionIndex = 1 });
        await game.ActAsync("Lydia", "answer", new { optionIndex = 1 });
        await game.ActAsync("James", "answer", new { optionIndex = 1 });

        var view = Payload(await game.ActAsync("Jacob", "answer", new { optionIndex = 1 }));

        Assert.Null(view.CurrentQuestion!.Category);
    }

    [Fact]
    public void An_oversized_category_is_rejected()
    {
        var setup = new { questions = new[] { new { text = "Q", options = new[] { "a", "b" }, correctIndex = 0, category = new string('x', 41) } } };

        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(setup).Violation);
    }

    // ---- starter bank ----

    [Fact]
    public void The_starter_bank_is_sound()
    {
        var bank = ContentBank.Load<JsonElement>(TriviaEngine.StarterBank);

        Assert.True(bank.Count >= 30);
        Assert.Equal(bank.Count, bank.Select(q => q.GetProperty("text").GetString()).Distinct().Count());
        Assert.All(bank, q =>
        {
            var options = q.GetProperty("options").GetArrayLength();
            var correct = q.GetProperty("correctIndex").GetInt32();
            Assert.InRange(options, 2, 6);
            Assert.InRange(correct, 0, options - 1);
            Assert.False(string.IsNullOrWhiteSpace(q.GetProperty("category").GetString()));
        });
    }

    // ---- question packs ----

    private static readonly string[] EastAfricaCategories = ["Uganda", "Food", "Region"];

    [Fact]
    public void The_east_africa_pack_is_sound_and_fits_the_trivia_limits()
    {
        var bank = ContentBank.Load<JsonElement>(TriviaEngine.EastAfricaBank);

        Assert.Equal(20, bank.Count);
        Assert.Equal(bank.Count, bank.Select(q => q.GetProperty("text").GetString()).Distinct().Count());
        Assert.All(bank, q =>
        {
            var options = q.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToList();
            Assert.InRange(options.Count, 2, TriviaEngine.MaxOptionsPerQuestion);
            Assert.Equal(options.Count, options.Distinct().Count());
            Assert.All(options, o => Assert.InRange(o.Length, 1, TriviaEngine.MaxOptionLength));
            Assert.InRange(q.GetProperty("text").GetString()!.Length, 1, TriviaEngine.MaxQuestionLength);
            Assert.InRange(q.GetProperty("correctIndex").GetInt32(), 0, options.Count - 1);
            Assert.Contains(q.GetProperty("category").GetString(), EastAfricaCategories);
        });
        Assert.True(bank.Select(q => q.GetProperty("correctIndex").GetInt32()).Distinct().Count() >= 3, "the right answer should not always sit in the same place");
    }

    [Fact]
    public async Task The_host_can_play_the_east_africa_pack_and_every_question_comes_from_it()
    {
        using var game = Trivia(new { useBuiltIn = true, starterPack = "east-africa", builtInCount = 20 });
        var view = Payload(await game.StartAsync());
        Assert.Equal(20, view.TotalQuestions);

        var seen = new List<string?> { view.CurrentQuestion!.Category };
        for (var i = 1; i < 20; i++)
        {
            foreach (var player in game.Players) await game.ActAsync(player, "answer", new { optionIndex = 0 });
            seen.Add(Payload(await game.SnapshotAsync(game.Host)).CurrentQuestion?.Category);
        }

        Assert.All(seen.Where(c => c is not null), c => Assert.Contains(c, EastAfricaCategories));
    }

    [Fact]
    public async Task Without_a_pack_the_general_starter_set_is_used_as_before()
    {
        using var game = Trivia(new { useBuiltIn = true, builtInCount = 5 });

        var category = Payload(await game.StartAsync()).CurrentQuestion!.Category;

        Assert.DoesNotContain(category, EastAfricaCategories);
    }

    [Fact]
    public void An_unknown_pack_is_rejected()
    {
        var ex = SetupRejected(new { useBuiltIn = true, starterPack = "atlantis" });

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
        Assert.Contains("atlantis", ex.Message);
    }

    [Fact]
    public void Asking_for_more_questions_than_the_chosen_pack_has_is_rejected_but_all_of_it_is_fine()
    {
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { useBuiltIn = true, starterPack = "east-africa", builtInCount = 21 }).Violation);

        using var game = Trivia(new { useBuiltIn = true, starterPack = "east-africa", builtInCount = 20 });
        Assert.NotEqual(Guid.Empty, game.RoomId);
    }

    [Fact]
    public async Task The_built_in_bank_alone_plays_ten_questions_by_default()
    {
        using var game = Trivia(new { useBuiltIn = true });

        var view = Payload(await game.StartAsync());

        Assert.Equal(10, view.TotalQuestions);
        Assert.NotNull(view.CurrentQuestion!.Category);
    }

    [Fact]
    public async Task The_host_chooses_how_many_built_in_questions_and_they_follow_the_custom_ones()
    {
        using var game = Trivia(new { questions = Two, useBuiltIn = true, builtInCount = 5 });

        var view = Payload(await game.StartAsync());

        Assert.Equal(7, view.TotalQuestions);
        Assert.Equal("2 + 2?", view.CurrentQuestion!.Text);
    }

    [Fact]
    public void Nothing_at_all_is_still_rejected()
    {
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { questions = Array.Empty<object>() }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { useBuiltIn = false, questions = Array.Empty<object>() }).Violation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void The_built_in_count_must_fit_the_bank(int count) =>
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { useBuiltIn = true, builtInCount = count }).Violation);

    // ---- time limit ----

    [Fact]
    public async Task With_a_limit_each_question_gets_a_server_deadline()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });

        var view = Payload(await game.StartAsync());

        Assert.Equal(game.Clock.GetUtcNow().AddSeconds(20), view.Timer!.DeadlineAt);
        Assert.Equal(20, view.TimeLimitSeconds);
    }

    [Fact]
    public async Task Without_a_limit_there_is_no_timer()
    {
        using var game = Trivia(new { questions = Two });

        var view = Payload(await game.StartAsync());

        Assert.Null(view.Timer);
        Assert.Null(view.TimeLimitSeconds);
    }

    [Fact]
    public async Task An_answer_after_the_deadline_is_rejected()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromSeconds(21));

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { optionIndex = 1 }));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public async Task An_early_tick_changes_nothing()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromSeconds(19));

        var view = Payload(await game.ActAsync("Amos", "tick"));

        Assert.Equal(1, view.QuestionNumber);
    }

    [Fact]
    public async Task A_tick_after_the_deadline_moves_on_and_unanswered_players_score_nothing()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { optionIndex = 1 });
        game.Clock.Advance(TimeSpan.FromSeconds(21));

        var view = Payload(await game.ActAsync("Lydia", "tick"));

        Assert.Equal(2, view.QuestionNumber);
        Assert.Equal(1, view.Scoreboard.Single(s => s.Player == "Amos").Correct);
        Assert.Equal(0, view.Scoreboard.Single(s => s.Player == "Lydia").Correct);
        Assert.Equal(game.Clock.GetUtcNow().AddSeconds(20), view.Timer!.DeadlineAt);
    }

    [Fact]
    public async Task Ticking_past_the_last_question_completes_the_session()
    {
        using var game = Trivia(new { questions = new[] { Two[0] }, timeLimitSeconds = 20 });
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromSeconds(21));

        var snapshot = await game.ActAsync("Amos", "tick");

        Assert.Equal(SessionStatus.Completed, snapshot.Session.Status);
    }

    [Fact]
    public async Task A_tick_with_no_limit_does_nothing()
    {
        using var game = Trivia(new { questions = Two });
        await game.StartAsync();
        game.Clock.Advance(TimeSpan.FromHours(1));

        var view = Payload(await game.ActAsync("Amos", "tick"));

        Assert.Equal(1, view.QuestionNumber);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void The_time_limit_must_be_in_range(int seconds) =>
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { questions = Two, timeLimitSeconds = seconds }).Violation);

    [Fact]
    public async Task The_deadline_survives_a_restart()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });
        var started = Payload(await game.StartAsync());

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync("Amos"));

        Assert.Equal(started.Timer!.DeadlineAt, view.Timer!.DeadlineAt);
    }

    // ---- integrity ----

    [Theory]
    [InlineData("{\"optionIndex\":\"1\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task A_malformed_answer_is_invalid_input(string json)
    {
        using var game = Trivia(new { questions = Two });
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task The_current_question_never_carries_its_answer()
    {
        using var game = Trivia(new { questions = Two, timeLimitSeconds = 20 });
        var view = Payload(await game.StartAsync());

        var json = JsonSerializer.Serialize(view, view.GetType(), JsonSerializerOptions.Web);

        Assert.DoesNotContain("correctIndex", json);
    }

    [Fact]
    public async Task Simultaneous_final_answers_move_the_question_on_exactly_once()
    {
        using var game = Trivia(new { questions = Two });
        await game.StartAsync();
        var contexts = game.Players.Select(_ => game.Reopen()).ToList();

        try
        {
            await Task.WhenAll(game.Players.Select((p, i) => contexts[i].ActAsync(p, "answer", new { optionIndex = 1 })));
            var view = Payload(await game.SnapshotAsync("Amos"));

            Assert.Equal(2, view.QuestionNumber);
            Assert.Equal(SessionStatus.Active, (await game.SnapshotAsync("Amos")).Session.Status);
        }
        finally
        {
            contexts.ForEach(c => c.Dispose());
        }
    }
}
