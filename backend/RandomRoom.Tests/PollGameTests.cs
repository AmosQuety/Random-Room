using System.Text.Json;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class BuiltInBankTests
{
    [Theory]
    [InlineData("would-you-rather", 30)]
    [InlineData("this-or-that", 30)]
    [InlineData("most-likely-to", 30)]
    [InlineData("never-have-i-ever", 30)]
    [InlineData("survey-showdown", 15)]
    public void Each_bank_ships_enough_unique_items(string bank, int minimum)
    {
        var items = ContentBank.Load<JsonElement>(bank);

        Assert.True(items.Count >= minimum, $"{bank} has only {items.Count} items");
        Assert.Equal(items.Count, items.Select(i => i.GetRawText()).Distinct().Count());
    }

    [Fact]
    public void Every_built_in_prompt_passes_its_own_validation()
    {
        Assert.All(ContentBank.Load<JsonElement>("would-you-rather"), i => new WouldYouRatherRules().ParsePrompt(i));
        Assert.All(ContentBank.Load<JsonElement>("this-or-that"), i => new ThisOrThatRules().ParsePrompt(i));
        Assert.All(ContentBank.Load<JsonElement>("most-likely-to"), i => new MostLikelyToRules().ParsePrompt(i));
        Assert.All(ContentBank.Load<JsonElement>("never-have-i-ever"), i => new NeverHaveIEverRules().ParsePrompt(i));
        Assert.All(ContentBank.Load<JsonElement>("survey-showdown"), i => new SurveyShowdownRules().ParsePrompt(i));
    }
}

public class MostLikelyToTests
{
    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    [Fact]
    public async Task The_most_voted_player_scores_and_the_tally_is_revealed()
    {
        using var game = RoundGames.MostLikelyTo();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { player = "Lydia" });
        await game.ActAsync("Lydia", "answer", new { player = "James" });
        await game.ActAsync("James", "answer", new { player = "Lydia" });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { player = "Lydia" }));

        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(0, revealed.Scoreboard.Single(s => s.Player == "James").Score);
        Assert.Equal(3, revealed.Result!.Value.GetProperty("tally")[0].GetProperty("votes").GetInt32());
    }

    [Fact]
    public async Task A_tie_at_the_top_crowns_everyone_tied()
    {
        using var game = RoundGames.MostLikelyTo();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { player = "Lydia" });
        await game.ActAsync("Lydia", "answer", new { player = "Amos" });
        await game.ActAsync("James", "answer", new { player = "Lydia" });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { player = "Amos" }));

        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Amos").Score);
    }

    [Fact]
    public async Task You_cannot_vote_for_yourself_or_for_a_stranger()
    {
        using var game = RoundGames.MostLikelyTo();
        await game.StartAsync();

        var self = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { player = "Amos" }));
        var stranger = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { player = "Mallory" }));
        var oversized = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { player = new string('x', 41) }));

        Assert.All([self, stranger, oversized], ex => Assert.Equal(RuleViolation.InvalidInput, ex.Violation));
    }

    [Fact]
    public void Two_players_are_not_enough()
    {
        var ex = Assert.Throws<RoomRuleException>(() => { using var g = RoundGames.MostLikelyTo(players: ["Amos", "Lydia"]); });

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
        Assert.Contains("at least 3", ex.Message);
    }

    [Fact]
    public async Task Nobody_else_sees_who_you_voted_for_before_the_reveal()
    {
        using var game = RoundGames.MostLikelyTo();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { player = "Lydia" });

        var view = Payload(await game.SnapshotAsync("James"));

        Assert.Null(view.MyAnswer);
        Assert.Null(view.Result);
    }
}

public class NeverHaveIEverTests
{
    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    [Fact]
    public async Task Staying_clean_scores_and_confessions_are_listed_at_the_reveal()
    {
        using var game = RoundGames.NeverHaveIEver();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { have = true });
        await game.ActAsync("Lydia", "answer", new { have = false });
        await game.ActAsync("James", "answer", new { have = false });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { have = true }));

        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(0, revealed.Scoreboard.Single(s => s.Player == "Amos").Score);
        Assert.Equal(2, revealed.Result!.Value.GetProperty("have").GetArrayLength());
    }

    [Theory]
    [InlineData("{\"have\":\"yes\"}")]
    [InlineData("{\"have\":1}")]
    [InlineData("{}")]
    public async Task The_answer_must_be_true_or_false(string json)
    {
        using var game = RoundGames.NeverHaveIEver();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task The_session_completes_after_the_last_round()
    {
        using var game = RoundGames.NeverHaveIEver();
        await game.StartAsync();
        await game.ActAsync("Amos", "reveal");

        var done = await game.ActAsync("Amos", "next");

        Assert.Equal(RandomRoom.Api.Domain.SessionStatus.Completed, done.Session.Status);
    }
}

public class SurveyShowdownTests
{
    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    [Fact]
    public async Task The_board_is_never_sent_before_the_reveal()
    {
        using var game = RoundGames.Survey();
        var started = Payload(await game.StartAsync());
        var json = JsonSerializer.Serialize(started, started.GetType(), JsonSerializerOptions.Web);

        Assert.DoesNotContain("Dog", json);
        Assert.DoesNotContain("Puppy", json);
        Assert.DoesNotContain("points", json);
        Assert.Equal(3, ((JsonElement)JsonSerializer.SerializeToElement(started.Prompt)).GetProperty("boardSize").GetInt32());
    }

    [Fact]
    public async Task Guesses_match_the_board_ignoring_case_and_aliases_and_score_the_answer_points()
    {
        using var game = RoundGames.Survey();
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { text = "  DOG! " });
        await game.ActAsync("Lydia", "answer", new { text = "puppy" });
        await game.ActAsync("James", "answer", new { text = "Cat" });
        var revealed = Payload(await game.ActAsync("Jacob", "answer", new { text = "Giraffe" }));

        Assert.Equal(40, revealed.Scoreboard.Single(s => s.Player == "Amos").Score);
        Assert.Equal(40, revealed.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(30, revealed.Scoreboard.Single(s => s.Player == "James").Score);
        Assert.Equal(0, revealed.Scoreboard.Single(s => s.Player == "Jacob").Score);
        Assert.Equal("Dog", revealed.Result!.Value.GetProperty("board")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("{\"text\":\"   \"}")]
    [InlineData("{\"text\":5}")]
    public async Task An_empty_or_non_text_guess_is_invalid(string json)
    {
        using var game = RoundGames.Survey();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task An_oversized_guess_is_rejected()
    {
        using var game = RoundGames.Survey();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { text = new string('x', 61) }));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public void A_survey_question_needs_a_board_with_points()
    {
        var noPoints = JsonSerializer.SerializeToElement(new { text = "Q", answers = new[] { new { text = "A" }, new { text = "B" } } });
        var tooShort = JsonSerializer.SerializeToElement(new { text = "Q", answers = new[] { new { text = "A", points = 5 } } });

        Assert.Equal(RuleViolation.InvalidInput, Assert.Throws<RoomRuleException>(() => new SurveyShowdownRules().ParsePrompt(noPoints)).Violation);
        Assert.Equal(RuleViolation.InvalidInput, Assert.Throws<RoomRuleException>(() => new SurveyShowdownRules().ParsePrompt(tooShort)).Violation);
    }
}

public class ThisOrThatTests
{
    [Fact]
    public async Task It_plays_like_a_two_way_poll_with_its_own_content()
    {
        using var game = new GameHarness(d => new RoundGameEngine<TwoWayPrompt>(new ThisOrThatRules(), d.Store, d.Random, d.Clock),
            "this-or-that", new { useBuiltIn = true, rounds = 3 });
        var started = (RoundPayload)(await game.StartAsync()).GamePayload;

        Assert.Equal(3, started.TotalRounds);
        await game.ActAsync("Amos", "answer", new { choice = 1 });
        await game.ActAsync("Lydia", "answer", new { choice = 1 });
        await game.ActAsync("James", "answer", new { choice = 0 });
        var revealed = (RoundPayload)(await game.ActAsync("Jacob", "answer", new { choice = 1 })).GamePayload;

        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == "Amos").Score);
    }
}
