using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.SketchGuess;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class SketchGuessTests
{
    private static GameHarness NewGame(object? setup = null, string[]? players = null) =>
        new(d => new SketchGuessEngine(d.Store, d.Random, d.Clock), SketchGuessEngine.Key,
            setup ?? new { words = new[] { "Sailboat", "Cactus" }, useBuiltIn = false, rounds = 2, timeLimitSeconds = 60 }, players);

    private static SketchPayload Payload(RoomSnapshot snapshot) => (SketchPayload)snapshot.GamePayload;

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    private sealed record Round(GameHarness Game, string Drawer, string[] Guessers, string Word);

    private static async Task<Round> StartRoundAsync(GameHarness game)
    {
        await game.StartAsync();
        return await ReadRoundAsync(game);
    }

    private static async Task<Round> ReadRoundAsync(GameHarness game)
    {
        var drawer = Payload(await game.SnapshotAsync("Amos")).Drawer!;
        var word = Payload(await game.SnapshotAsync(drawer)).Word!;
        return new Round(game, drawer, game.Players.Where(p => p != drawer).ToArray(), word);
    }

    private static object Line(int color = 0, int size = 4, params int[] points) =>
        new { color, size, points = points.Length == 0 ? [10, 10, 200, 200] : points };

    private static Task DrawAsync(Round r, params object[] strokes) => r.Game.ActAsync(r.Drawer, "strokes", new { strokes });

    /// <summary>Moves the clock past the canvas rate limit so the next canvas action is allowed.</summary>
    private static void Pause(Round r) => r.Game.Clock.Advance(SketchGuessEngine.MinCanvasGap + TimeSpan.FromMilliseconds(10));

    [Fact]
    public async Task Only_the_drawer_is_ever_sent_the_word()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);

        foreach (var player in game.Players)
        {
            var view = Payload(await game.SnapshotAsync(player));
            var json = JsonSerializer.Serialize(view, JsonSerializerOptions.Web);
            if (player == r.Drawer) Assert.Equal(r.Word, view.Word);
            else
            {
                Assert.Null(view.Word);
                Assert.DoesNotContain(r.Word, json, StringComparison.OrdinalIgnoreCase);
            }
        }
        var publicJson = JsonSerializer.Serialize(Payload(await game.PublicSnapshotAsync()), JsonSerializerOptions.Web);
        Assert.DoesNotContain(r.Word, publicJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_preview_never_names_a_word()
    {
        using var game = NewGame();
        var engine = new SketchGuessEngine(new GameStore(game.Db, game.Clock), new ScriptedRandom(), game.Clock);

        var preview = JsonSerializer.Serialize(await engine.GetRoomPreviewAsync(game.RoomId, default));

        Assert.DoesNotContain("Sailboat", preview);
        Assert.DoesNotContain("Cactus", preview);
    }

    [Fact]
    public async Task Turns_rotate_and_a_new_round_starts_with_a_blank_canvas()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line());
        await game.ActAsync("Amos", "reveal");

        var next = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(2, next.Round);
        Assert.NotEqual(r.Drawer, next.Drawer);
        Assert.Empty(next.Strokes);
    }

    // ---- the canvas ----

    [Fact]
    public async Task The_drawers_strokes_are_stored_and_seen_by_every_player()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);

        await DrawAsync(r, Line(2, 6, 0, 0, 100, 100, 200, 50), Line(3, 8, 500, 500));

        foreach (var player in game.Players)
        {
            var strokes = Payload(await game.SnapshotAsync(player)).Strokes;
            Assert.Equal(2, strokes.Count);
            Assert.Equal(new Stroke(2, 6, [0, 0, 100, 100, 200, 50]), strokes[0], StrokeComparer.Instance);
            Assert.Equal([500, 500], strokes[1].Points);
        }
        Assert.Equal(2, Payload(await game.PublicSnapshotAsync()).Strokes.Count);
    }

    [Fact]
    public async Task Only_the_drawer_can_draw_undo_or_clear()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var guesser = r.Guessers[0];

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(guesser, "strokes", new { strokes = new[] { Line() } })));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(guesser, "undo")));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(guesser, "clear")));
        Assert.Empty(Payload(await game.SnapshotAsync("Amos")).Strokes);
    }

    [Fact]
    public async Task Undo_removes_the_last_stroke_and_clear_removes_everything()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line(0), Line(1), Line(2));

        Pause(r);
        var undone = Payload(await game.ActAsync(r.Drawer, "undo"));
        Assert.Equal([0, 1], undone.Strokes.Select(s => s.Color));

        Pause(r);
        var cleared = Payload(await game.ActAsync(r.Drawer, "clear"));
        Assert.Empty(cleared.Strokes);

        Pause(r);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Drawer, "undo")));
    }

    [Fact]
    public async Task Canvas_actions_that_come_too_fast_are_refused_and_allowed_again_after_the_gap()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line());

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => DrawAsync(r, Line())));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Drawer, "undo")));
        Assert.Single(Payload(await game.SnapshotAsync("Amos")).Strokes);

        Pause(r);
        await DrawAsync(r, Line());
        Assert.Equal(2, Payload(await game.SnapshotAsync("Amos")).Strokes.Count);
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("empty")]
    [InlineData("nocolor")]
    [InlineData("color8")]
    [InlineData("colorneg")]
    [InlineData("size0")]
    [InlineData("size25")]
    [InlineData("nopoints")]
    [InlineData("oddpoints")]
    [InlineData("negpoint")]
    [InlineData("bigpoint")]
    [InlineData("fractional")]
    [InlineData("textpoint")]
    [InlineData("toolong")]
    [InlineData("bigbatch")]
    [InlineData("notobject")]
    public async Task Malformed_or_oversized_strokes_are_rejected_and_nothing_is_stored(string kind)
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var tooManyPoints = Enumerable.Repeat(5, (SketchGuessEngine.MaxPointsPerStroke + 1) * 2).ToArray();

        Task Send(object? payload) => game.ActAsync(r.Drawer, "strokes", payload);
        Task attempt = kind switch
        {
            "nobody" => Send(null),
            "empty" => Send(new { strokes = Array.Empty<object>() }),
            "nocolor" => Send(new { strokes = new object[] { new { size = 4, points = new[] { 1, 1 } } } }),
            "color8" => Send(new { strokes = new[] { Line(SketchGuessEngine.PaletteSize) } }),
            "colorneg" => Send(new { strokes = new[] { Line(-1) } }),
            "size0" => Send(new { strokes = new[] { Line(0, 0) } }),
            "size25" => Send(new { strokes = new[] { Line(0, SketchGuessEngine.MaxPenSize + 1) } }),
            "nopoints" => Send(new { strokes = new object[] { new { color = 0, size = 4, points = Array.Empty<int>() } } }),
            "oddpoints" => Send(new { strokes = new[] { Line(0, 4, 1, 2, 3) } }),
            "negpoint" => Send(new { strokes = new[] { Line(0, 4, -1, 5) } }),
            "bigpoint" => Send(new { strokes = new[] { Line(0, 4, 5, SketchGuessEngine.GridSize + 1) } }),
            "fractional" => Send(new { strokes = new object[] { new { color = 0, size = 4, points = new[] { 1.5, 2.0 } } } }),
            "textpoint" => Send(new { strokes = new object[] { new { color = 0, size = 4, points = new object[] { "a", "b" } } } }),
            "toolong" => Send(new { strokes = new[] { Line(0, 4, tooManyPoints) } }),
            "bigbatch" => Send(new { strokes = Enumerable.Range(0, SketchGuessEngine.MaxStrokeBatch + 1).Select(_ => Line()).ToArray() }),
            _ => Send(new { strokes = 5 }),
        };

        Assert.Equal(RuleViolation.InvalidInput, (await GameHarness.RejectedAsync(() => attempt)).Violation);
        Assert.Empty(Payload(await game.SnapshotAsync("Amos")).Strokes);
    }

    [Fact]
    public async Task The_number_of_strokes_on_the_canvas_is_capped()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var batch = Enumerable.Range(0, SketchGuessEngine.MaxStrokeBatch).Select(_ => Line()).ToArray();
        for (var i = 0; i < SketchGuessEngine.MaxStrokes / SketchGuessEngine.MaxStrokeBatch; i++)
        {
            Pause(r);
            await DrawAsync(r, batch);
        }

        Pause(r);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => DrawAsync(r, Line())));
        Assert.Equal(SketchGuessEngine.MaxStrokes, Payload(await game.SnapshotAsync("Amos")).Strokes.Count);

        Pause(r);
        await game.ActAsync(r.Drawer, "undo");
        Pause(r);
        await DrawAsync(r, Line());
    }

    [Fact]
    public async Task The_total_detail_of_the_drawing_is_capped()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var dense = Line(0, 4, Enumerable.Range(0, SketchGuessEngine.MaxPointsPerStroke * 2).Select(i => i % 1000).ToArray());
        for (var i = 0; i < SketchGuessEngine.MaxTotalPoints / SketchGuessEngine.MaxPointsPerStroke; i++)
        {
            Pause(r);
            await DrawAsync(r, dense);
        }

        Pause(r);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => DrawAsync(r, Line())));
    }

    [Fact]
    public async Task Nothing_can_be_drawn_once_the_round_is_over()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await game.ActAsync("Amos", "reveal");
        Pause(r);

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => DrawAsync(r, Line())));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Drawer, "clear")));
    }

    // ---- guessing ----

    [Fact]
    public async Task The_first_correct_guess_scores_two_for_the_guesser_and_one_for_the_drawer_matching_loosely()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);

        await game.ActAsync(r.Guessers[0], "guess", new { text = "a dinghy" });
        var done = Payload(await game.ActAsync(r.Guessers[1], "guess", new { text = $"  {r.Word.ToUpperInvariant()}! " }));

        Assert.Equal(Phases.Revealed, done.Phase);
        Assert.Equal("guessed", done.Result!.Value.GetProperty("outcome").GetString());
        Assert.Equal(2, done.Scoreboard.Single(s => s.Player == r.Guessers[1]).Score);
        Assert.Equal(1, done.Scoreboard.Single(s => s.Player == r.Drawer).Score);
        Assert.Equal(0, done.Scoreboard.Single(s => s.Player == r.Guessers[0]).Score);
    }

    [Fact]
    public async Task Wrong_guesses_are_public_and_the_round_stays_open()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);

        await game.ActAsync(r.Guessers[0], "guess", new { text = "a dinghy" });
        var view = Payload(await game.SnapshotAsync(r.Guessers[1]));

        Assert.Equal(SketchGuessEngine.Drawing, view.Phase);
        Assert.Equal([new SketchGuessView(r.Guessers[0], "a dinghy")], view.Guesses);
    }

    [Fact]
    public async Task The_drawer_cannot_guess_and_bad_or_excessive_guesses_are_rejected()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var guesser = r.Guessers[0];

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Drawer, "guess", new { text = r.Word })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess")));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = " " })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = 7 })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = new string('x', SketchGuessEngine.MaxGuessLength + 1) })));

        for (var i = 0; i < SketchGuessEngine.MaxGuessesPerRound; i++) await game.ActAsync(guesser, "guess", new { text = $"nope {i}" });
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(guesser, "guess", new { text = "one more" })));
    }

    [Fact]
    public async Task Guessing_after_the_round_ended_is_refused()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await game.ActAsync(r.Guessers[0], "guess", new { text = r.Word });

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(r.Guessers[1], "guess", new { text = r.Word })));
    }

    [Fact]
    public async Task Two_correct_guesses_at_once_score_only_one()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        var other = game.Reopen();

        var results = await Task.WhenAll(Try(game, r.Guessers[0], r.Word), Try(other, r.Guessers[1], r.Word));
        other.Dispose();

        Assert.Single(results, ok => ok);
        Assert.Equal(3, Payload(await game.SnapshotAsync("Amos")).Scoreboard.Sum(s => s.Score));

        static async Task<bool> Try(GameHarness g, string p, string w)
        {
            try { await g.ActAsync(p, "guess", new { text = w }); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    // ---- rounds ----

    [Fact]
    public async Task Only_the_drawer_can_skip_and_only_the_host_can_reveal_or_move_on()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(r.Guessers[0], "skip")));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "reveal")));
        var skipped = Payload(await game.ActAsync(r.Drawer, "skip"));

        Assert.Equal("skipped", skipped.Result!.Value.GetProperty("outcome").GetString());
        Assert.Equal(r.Word, skipped.Result.Value.GetProperty("word").GetString());
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "next")));
    }

    [Fact]
    public async Task The_word_is_shown_to_everyone_after_the_round_and_the_drawing_stays_until_the_next_one()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line());
        await game.ActAsync("Amos", "reveal");

        var view = Payload(await game.SnapshotAsync(r.Guessers[0]));

        Assert.Equal(r.Word, view.Result!.Value.GetProperty("word").GetString());
        Assert.Single(view.Strokes);
    }

    [Fact]
    public async Task The_game_completes_after_the_last_round_and_no_drawing_is_kept()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line());
        await game.ActAsync("Amos", "reveal");
        await game.ActAsync("Amos", "next");
        await game.ActAsync("Amos", "reveal");

        var done = Payload(await game.ActAsync("Amos", "next"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Empty(done.Strokes);
        Assert.Null(done.Word);
        Assert.Null(done.Drawer);
        var stored = (await game.Db.GameSessionStates.AsNoTracking().SingleAsync()).DataJson;
        Assert.Equal(0, JsonDocument.Parse(stored).RootElement.GetProperty("strokes").GetArrayLength());
    }

    [Fact]
    public async Task The_server_ends_the_round_only_after_its_own_clock_passes_the_deadline()
    {
        using var game = NewGame();
        await game.StartAsync();

        await game.ActAsync("Lydia", "tick");
        Assert.Equal(SketchGuessEngine.Drawing, Payload(await game.SnapshotAsync("Amos")).Phase);

        game.Clock.Advance(TimeSpan.FromSeconds(61));
        await game.ActAsync("Lydia", "tick");

        var view = Payload(await game.SnapshotAsync("Amos"));
        Assert.Equal(Phases.Revealed, view.Phase);
        Assert.Equal("time", view.Result!.Value.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task The_drawing_the_word_and_the_guesses_survive_a_restart()
    {
        using var game = NewGame();
        var r = await StartRoundAsync(game);
        await DrawAsync(r, Line(1, 5, 1, 2, 3, 4));
        await game.ActAsync(r.Guessers[0], "guess", new { text = "a dinghy" });

        using var restarted = game.Reopen();

        Assert.Equal(r.Word, Payload(await restarted.SnapshotAsync(r.Drawer)).Word);
        var guesserView = Payload(await restarted.SnapshotAsync(r.Guessers[1]));
        Assert.Equal([1, 2, 3, 4], guesserView.Strokes.Single().Points);
        Assert.Single(guesserView.Guesses);
    }

    // ---- setup ----

    [Fact]
    public async Task Built_in_words_are_a_bank_of_about_a_hundred_and_are_used_when_asked_for()
    {
        using var game = NewGame(new { useBuiltIn = true, rounds = 5 });
        var view = Payload(await game.StartAsync());
        var bank = ContentBank.Load<string>("sketch-guess");

        Assert.Equal(5, view.TotalRounds);
        Assert.InRange(bank.Count, 100, 150);
        Assert.Equal(bank.Count, bank.Select(AnswerNormalizer.Normalize).Distinct().Count());
        Assert.Equal(90, view.TimeLimitSeconds);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("blank")]
    [InlineData("symbols")]
    [InlineData("long")]
    [InlineData("dupes")]
    [InlineData("nontext")]
    [InlineData("many")]
    [InlineData("rounds")]
    [InlineData("time")]
    public void Invalid_setup_is_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "none" => new { words = Array.Empty<string>(), useBuiltIn = false },
            "blank" => new { words = new[] { " " } },
            "symbols" => new { words = new[] { "!!!" } },
            "long" => new { words = new[] { new string('x', SketchGuessEngine.MaxWordLength + 1) } },
            "dupes" => new { words = new[] { "Cat", " cat! " } },
            "nontext" => new { words = new object[] { 3 } },
            "many" => new { words = Enumerable.Range(0, SketchGuessEngine.MaxCustomWords + 1).Select(i => $"w{i}") },
            "rounds" => new { words = new[] { "Cat" }, rounds = 0 },
            _ => new { words = new[] { "Cat" }, timeLimitSeconds = 10 },
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

    private sealed class StrokeComparer : IEqualityComparer<Stroke>
    {
        public static readonly StrokeComparer Instance = new();

        public bool Equals(Stroke? a, Stroke? b) => a is not null && b is not null && a.Color == b.Color && a.Size == b.Size && a.Points.SequenceEqual(b.Points);

        public int GetHashCode(Stroke s) => HashCode.Combine(s.Color, s.Size, s.Points.Count);
    }
}
