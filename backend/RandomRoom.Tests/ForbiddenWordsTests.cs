using System.Text.Json;
using RandomRoom.Api.Games.ForbiddenWords;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class ForbiddenWordsTests
{
    private static readonly object[] Cards =
    [
        new { word = "Bicycle", forbidden = new[] { "wheel", "pedal", "ride" } },
        new { word = "Pizza", forbidden = new[] { "cheese", "slice" } },
    ];

    private static GameHarness NewGame(object? setup = null, string[]? players = null) =>
        new(d => new ForbiddenWordsEngine(d.Store, d.Random, d.Clock), ForbiddenWordsEngine.Key,
            setup ?? new { cards = Cards, useBuiltIn = false, rounds = 2, timeLimitSeconds = 60 }, players);

    private static ForbiddenPayload Payload(RoomSnapshot snapshot) => (ForbiddenPayload)snapshot.GamePayload;

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    private sealed record Round(GameHarness Game, string Describer, string Judge, string[] Guessers, string Word);

    private static async Task<Round> ReadRoundAsync(GameHarness game)
    {
        var any = Payload(await game.SnapshotAsync("Amos"));
        var describer = any.Describer!;
        var judge = any.Judge!;
        var word = Payload(await game.SnapshotAsync(describer)).Card!.Word;
        return new Round(game, describer, judge, game.Players.Where(p => p != describer && p != judge).ToArray(), word);
    }

    [Fact]
    public async Task A_round_names_a_describer_and_a_judge_and_turns_rotate()
    {
        using var game = NewGame();
        var first = Payload(await game.StartAsync());
        var players = first.Scoreboard.Select(s => s.Player).ToList();

        Assert.Equal(ForbiddenWordsEngine.Playing, first.Phase);
        Assert.Equal(players[0], first.Describer);
        Assert.Equal(players[1], first.Judge);

        await game.ActAsync("Amos", "reveal");
        var second = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(players[1], second.Describer);
        Assert.Equal(players[2], second.Judge);
        Assert.Equal(2, second.Round);
    }

    [Fact]
    public async Task Only_the_describer_and_the_judge_ever_receive_the_card()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        foreach (var player in game.Players)
        {
            var view = Payload(await game.SnapshotAsync(player));
            var json = JsonSerializer.Serialize(view, JsonSerializerOptions.Web);
            if (player == r.Describer || player == r.Judge)
            {
                Assert.Equal(r.Word, view.Card!.Word);
                Assert.NotEmpty(view.Card.Forbidden);
            }
            else
            {
                Assert.Null(view.Card);
                Assert.DoesNotContain(r.Word, json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("forbidden\":[\"", json);
                Assert.DoesNotContain("wheel", json);
                Assert.DoesNotContain("cheese", json);
            }
        }
    }

    [Fact]
    public async Task The_public_snapshot_and_preview_never_carry_a_card()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        var view = Payload(await game.PublicSnapshotAsync());
        var json = JsonSerializer.Serialize(view, JsonSerializerOptions.Web);
        var engine = new ForbiddenWordsEngine(new GameStore(game.Db, game.Clock), new ScriptedRandom(), game.Clock);
        var preview = JsonSerializer.Serialize(await engine.GetRoomPreviewAsync(game.RoomId, default));

        Assert.Null(view.Card);
        Assert.DoesNotContain(r.Word, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bicycle", preview);
        Assert.DoesNotContain("Pizza", preview);
    }

    [Fact]
    public async Task A_correct_guess_scores_the_guesser_and_the_describer_and_ends_the_round_matching_loosely()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        await game.ActAsync(r.Guessers[0], "guess", new { text = "a scooter" });
        var done = Payload(await game.ActAsync(r.Guessers[1], "guess", new { text = $"  {r.Word.ToUpperInvariant()}! " }));

        Assert.Equal(Phases.Revealed, done.Phase);
        Assert.Equal("guessed", done.Result!.Value.GetProperty("outcome").GetString());
        Assert.Equal(r.Guessers[1], done.Result.Value.GetProperty("guesser").GetString());
        Assert.Equal(1, done.Scoreboard.Single(s => s.Player == r.Guessers[1]).Score);
        Assert.Equal(1, done.Scoreboard.Single(s => s.Player == r.Describer).Score);
        Assert.Equal(0, done.Scoreboard.Single(s => s.Player == r.Judge).Score);
    }

    [Fact]
    public async Task Wrong_guesses_are_public_and_the_round_stays_open()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        await game.ActAsync(r.Guessers[0], "guess", new { text = "a scooter" });
        var view = Payload(await game.SnapshotAsync(r.Guessers[1]));

        Assert.Equal(ForbiddenWordsEngine.Playing, view.Phase);
        Assert.Equal([new GuessView(r.Guessers[0], "a scooter")], view.Guesses);
    }

    [Fact]
    public async Task The_describer_and_the_judge_cannot_guess()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Describer, "guess", new { text = r.Word })));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Judge, "guess", new { text = r.Word })));
    }

    [Fact]
    public async Task Malformed_guesses_are_rejected_and_the_number_of_guesses_is_capped()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);
        var guesser = r.Guessers[0];

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess")));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = " " })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = 4 })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = new string('x', ForbiddenWordsEngine.MaxGuessLength + 1) })));

        for (var i = 0; i < ForbiddenWordsEngine.MaxGuessesPerRound; i++) await game.ActAsync(guesser, "guess", new { text = $"nope {i}" });
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = "one more" })));
        Assert.Equal(0, Payload(await game.SnapshotAsync(guesser)).MyGuessesLeft);
    }

    [Fact]
    public async Task The_judge_or_the_host_can_flag_a_slip_which_costs_the_describer_a_point_but_never_below_zero()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Describer, "flag")));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Guessers[0] == "Amos" ? r.Guessers[1] : r.Guessers[0], "flag")));
        var flagged = Payload(await game.ActAsync(r.Judge, "flag"));

        Assert.Equal(Phases.Revealed, flagged.Phase);
        Assert.Equal("flagged", flagged.Result!.Value.GetProperty("outcome").GetString());
        Assert.Equal(0, flagged.Scoreboard.Single(s => s.Player == r.Describer).Score);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Judge, "flag")));
    }

    [Fact]
    public async Task A_flag_takes_a_point_off_a_describer_who_had_one()
    {
        // With 3 players the describer of round 4 is the describer of round 1.
        using var game = NewGame(new { useBuiltIn = true, rounds = 4 }, ["Amos", "Jacob", "James"]);
        await game.StartAsync();
        var first = await ReadRoundAsync(game);
        await game.ActAsync(first.Guessers[0], "guess", new { text = first.Word });
        Assert.Equal(1, Payload(await game.SnapshotAsync("Amos")).Scoreboard.Single(s => s.Player == first.Describer).Score);
        for (var i = 0; i < 2; i++)
        {
            await game.ActAsync("Amos", "next");
            await game.ActAsync("Amos", "reveal");
        }
        var last = Payload(await game.ActAsync("Amos", "next"));
        Assert.Equal(first.Describer, last.Describer);

        var flagged = Payload(await game.ActAsync(last.Judge!, "flag"));

        Assert.Equal(0, flagged.Scoreboard.Single(s => s.Player == first.Describer).Score);
        Assert.Equal(1, flagged.Scoreboard.Single(s => s.Player == first.Guessers[0]).Score);
    }

    [Fact]
    public async Task Only_the_describer_can_skip_the_card()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Guessers[0], "skip")));
        var skipped = Payload(await game.ActAsync(r.Describer, "skip"));

        Assert.Equal("skipped", skipped.Result!.Value.GetProperty("outcome").GetString());
        Assert.All(skipped.Scoreboard, s => Assert.Equal(0, s.Score));
    }

    [Fact]
    public async Task Only_the_host_can_reveal_and_move_on_and_the_card_is_shown_to_everyone_after_the_round()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "reveal")));
        var revealed = Payload(await game.ActAsync("Amos", "reveal"));

        Assert.Equal(r.Word, revealed.Result!.Value.GetProperty("word").GetString());
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "next")));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "reveal")));
        Assert.Null(Payload(await game.SnapshotAsync(r.Guessers[0])).Card);
    }

    [Fact]
    public async Task Nothing_can_be_done_in_the_wrong_phase()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);
        await game.ActAsync("Amos", "reveal");

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Guessers[0], "guess", new { text = "x" })));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Describer, "skip")));
    }

    [Fact]
    public async Task The_game_completes_after_the_last_round()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "reveal");
        await game.ActAsync("Amos", "next");
        await game.ActAsync("Amos", "reveal");

        var done = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Null(done.Describer);
        Assert.Null(done.Card);
    }

    [Fact]
    public async Task The_server_ends_the_round_only_after_its_own_clock_passes_the_deadline()
    {
        using var game = NewGame();
        await game.StartAsync();

        await game.ActAsync("Lydia", "tick");
        Assert.Equal(ForbiddenWordsEngine.Playing, Payload(await game.SnapshotAsync("Amos")).Phase);

        game.Clock.Advance(TimeSpan.FromSeconds(61));
        await game.ActAsync("Lydia", "tick");
        var timed = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(Phases.Revealed, timed.Phase);
        Assert.Equal("time", timed.Result!.Value.GetProperty("outcome").GetString());
        await game.ActAsync("Lydia", "tick");
        Assert.Equal(Phases.Revealed, Payload(await game.SnapshotAsync("Amos")).Phase);
    }

    [Fact]
    public async Task Two_correct_guesses_at_once_score_only_one_of_them()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);
        var other = game.Reopen();

        var results = await Task.WhenAll(Try(game, r.Guessers[0], r.Word), Try(other, r.Guessers[1], r.Word));
        other.Dispose();

        Assert.Single(results, ok => ok);
        var view = Payload(await game.SnapshotAsync("Amos"));
        Assert.Equal(2, view.Scoreboard.Sum(s => s.Score));

        static async Task<bool> Try(GameHarness g, string p, string w)
        {
            try { await g.ActAsync(p, "guess", new { text = w }); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    [Fact]
    public async Task The_card_the_round_and_the_guesses_survive_a_restart()
    {
        using var game = NewGame();
        await game.StartAsync();
        var r = await ReadRoundAsync(game);
        await game.ActAsync(r.Guessers[0], "guess", new { text = "a scooter" });

        using var restarted = game.Reopen();

        Assert.Equal(r.Word, Payload(await restarted.SnapshotAsync(r.Describer)).Card!.Word);
        Assert.Single(Payload(await restarted.SnapshotAsync(r.Guessers[1])).Guesses);
    }

    [Fact]
    public void The_game_needs_three_players()
    {
        Assert.Throws<RoomRuleException>(() => NewGame(players: ["Amos", "Lydia"]));
    }

    [Fact]
    public async Task Built_in_cards_are_used_when_asked_for()
    {
        using var game = NewGame(new { useBuiltIn = true, rounds = 5 });
        var view = Payload(await game.StartAsync());
        var bank = ContentBank.Load<TabooCard>("forbidden-words");

        Assert.Equal(5, view.TotalRounds);
        Assert.Equal(40, bank.Count);
        Assert.All(bank, c =>
        {
            Assert.InRange(c.Forbidden.Count, 1, ForbiddenWordsEngine.MaxForbidden);
            Assert.DoesNotContain(c.Forbidden, f => AnswerNormalizer.Normalize(f) == AnswerNormalizer.Normalize(c.Word));
        });
    }

    [Theory]
    [InlineData("none")]
    [InlineData("noforbidden")]
    [InlineData("selfforbidden")]
    [InlineData("longword")]
    [InlineData("dupes")]
    [InlineData("nonobject")]
    [InlineData("rounds")]
    [InlineData("time")]
    public void Invalid_setup_is_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "none" => new { cards = Array.Empty<object>(), useBuiltIn = false },
            "noforbidden" => new { cards = new object[] { new { word = "Cat", forbidden = Array.Empty<string>() } } },
            "selfforbidden" => new { cards = new object[] { new { word = "Cat", forbidden = new[] { "cat!" } } } },
            "longword" => new { cards = new object[] { new { word = new string('x', ForbiddenWordsEngine.MaxWordLength + 1), forbidden = new[] { "a" } } } },
            "dupes" => new { cards = new object[] { new { word = "Cat", forbidden = new[] { "a" } }, new { word = " cat", forbidden = new[] { "b" } } } },
            "nonobject" => new { cards = new object[] { "Cat" } },
            "rounds" => new { cards = Cards, rounds = 0 },
            _ => new { cards = Cards, timeLimitSeconds = 5 },
        };

        Assert.Throws<RoomRuleException>(() => NewGame(setup));
    }

    [Fact]
    public async Task An_unknown_action_is_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Amos", "peek")));
    }
}
