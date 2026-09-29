using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.WordSpies;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class WordSpiesTests
{
    private static readonly string[] SixPlayers = ["Amos", "Lydia", "James", "Jacob", "Ruth", "Sam"];

    private static GameHarness NewGame(object? setup = null, string[]? players = null, int seed = 7) =>
        new(d => new WordSpiesEngine(d.Store, d.Random), WordSpiesEngine.Key, setup ?? new { useBuiltIn = true }, players ?? SixPlayers, new SeededRandom(seed));

    private static WordSpiesPayload Payload(RoomSnapshot snapshot) => (WordSpiesPayload)snapshot.GamePayload;

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    /// <summary>Everyone's view, so a test can find the roles and read the key the way a spymaster would.</summary>
    private sealed record Table(GameHarness Game, Dictionary<string, WordSpiesPayload> Views)
    {
        public IReadOnlyList<string> Key => Views.Values.First(v => v.IsSpymaster).Key!;
        public string Turn => Views.Values.First().Turn!;
        public string Spymaster(string team) => Views.First(v => v.Value.IsSpymaster && v.Value.MyTeam == team).Key;
        public string Operative(string team) => Views.First(v => !v.Value.IsSpymaster && v.Value.MyTeam == team).Key;
        public string OperativeOf(string team, int skip) => Views.Where(v => !v.Value.IsSpymaster && v.Value.MyTeam == team).Skip(skip).First().Key;
        public IEnumerable<int> CellsOf(string owner) => Enumerable.Range(0, WordSpiesEngine.Cells).Where(i => Key[i] == owner);
    }

    private static async Task<Table> StartAsync(GameHarness game)
    {
        await game.StartAsync();
        return await ReadAsync(game);
    }

    private static async Task<Table> ReadAsync(GameHarness game)
    {
        var views = new Dictionary<string, WordSpiesPayload>();
        foreach (var player in game.Players) views[player] = Payload(await game.SnapshotAsync(player));
        return new Table(game, views);
    }

    private static async Task<string> TurnNowAsync(Table t) => Payload(await t.Game.SnapshotAsync("Amos")).Turn!;

    /// <summary>The spymaster of whichever team is in play right now gives the clue.</summary>
    private static async Task ClueAsync(Table t, string word = "animals", int count = 3) =>
        await t.Game.ActAsync(t.Spymaster(await TurnNowAsync(t)), "clue", new { word, count });

    /// <summary>An operative of whichever team is in play right now (or the named player) turns a card.</summary>
    private static async Task GuessAsync(Table t, int cell, string? by = null) =>
        await t.Game.ActAsync(by ?? t.Operative(await TurnNowAsync(t)), "guess", new { cell });

    [Fact]
    public async Task The_board_is_dealt_with_the_classic_mix_and_the_starting_team_has_the_extra_card()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.Equal(WordSpiesEngine.Cells, t.Views["Amos"].Board.Count);
        Assert.Equal(WordSpiesEngine.Cells, t.Views["Amos"].Board.Select(c => c.Word).Distinct().Count());
        Assert.Equal(1, t.Key.Count(k => k == Owner.Assassin));
        Assert.Equal(7, t.Key.Count(k => k == Owner.Neutral));
        Assert.Equal(9, t.Key.Count(k => k == t.Turn));
        Assert.Equal(8, t.Key.Count(k => k == Team.Other(t.Turn)));
        Assert.Equal(WordSpiesEngine.GivingClue, t.Views["Amos"].Phase);
    }

    [Fact]
    public async Task Everyone_is_on_a_team_and_each_team_has_one_spymaster_and_at_least_one_operative()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.All(t.Views.Values, v => Assert.NotNull(v.MyTeam));
        foreach (var team in new[] { Team.Red, Team.Blue })
        {
            var members = t.Views.Where(v => v.Value.MyTeam == team).ToList();
            Assert.Equal(1, members.Count(m => m.Value.IsSpymaster));
            Assert.True(members.Count >= 2);
        }
        Assert.Equal(6, t.Views["Amos"].Teams.Values.Sum(m => m.Count));
    }

    [Fact]
    public async Task Only_the_two_spymasters_receive_the_key_and_nobody_else_can_see_unrevealed_owners()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.Equal(2, t.Views.Values.Count(v => v.Key is not null));
        Assert.All(t.Views.Values.Where(v => v.IsSpymaster), v => Assert.Equal(t.Key, v.Key));
        foreach (var view in t.Views.Values)
        {
            Assert.All(view.Board, c => Assert.Null(c.Owner));
            if (view.IsSpymaster) continue;
            Assert.Null(view.Key);
            var json = JsonSerializer.Serialize(view, JsonSerializerOptions.Web);
            Assert.DoesNotContain("assassin", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("neutral", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_public_view_and_the_stored_key_never_reach_the_public_snapshot()
    {
        using var game = NewGame();
        await StartAsync(game);

        var publicView = Payload(await game.PublicSnapshotAsync());
        var json = JsonSerializer.Serialize(publicView, JsonSerializerOptions.Web);

        Assert.Null(publicView.Key);
        Assert.All(publicView.Board, c => Assert.Null(c.Owner));
        Assert.DoesNotContain("assassin", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(publicView.MyTeam);
    }

    [Fact]
    public async Task A_revealed_card_shows_its_owner_to_everyone_and_nothing_else_does()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        var cell = t.CellsOf(team).First();
        await ClueAsync(t);
        await GuessAsync(t, cell);

        var after = await ReadAsync(game);

        foreach (var view in after.Views.Values)
        {
            Assert.Equal(team, view.Board[cell].Owner);
            Assert.Equal(1, view.Board.Count(c => c.Owner is not null));
            if (!view.IsSpymaster) Assert.Null(view.Key);
        }
    }

    [Fact]
    public async Task Only_the_spymaster_of_the_team_in_play_can_give_the_clue()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var other = Team.Other(t.Turn);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(t.Spymaster(other), "clue", new { word = "animals", count = 2 })));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(t.Operative(t.Turn), "clue", new { word = "animals", count = 2 })));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(t.Operative(other), "clue", new { word = "animals", count = 2 })));
    }

    [Fact]
    public async Task A_clue_moves_the_game_to_guessing_and_is_shown_to_everyone()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        await ClueAsync(t, "animals", 3);
        var view = Payload(await game.SnapshotAsync(t.Operative(t.Turn)));

        Assert.Equal(WordSpiesEngine.Guessing, view.Phase);
        Assert.Equal("animals", view.Clue!.Word);
        Assert.Equal(3, view.Clue.Count);
        Assert.Equal(4, view.GuessesLeft);
        Assert.Single(view.ClueLog);
    }

    [Fact]
    public async Task A_second_clue_in_the_same_turn_is_refused()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        await ClueAsync(t);

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => ClueAsync(t, "again", 2)));
    }

    [Theory]
    [InlineData("two words", 2)]
    [InlineData("number5", 2)]
    [InlineData("", 2)]
    [InlineData("valid", 0)]
    [InlineData("valid", 10)]
    [InlineData("valid", -1)]
    public async Task Malformed_clues_are_rejected(string word, int count)
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => ClueAsync(t, word, count)));
        Assert.Equal(WordSpiesEngine.GivingClue, Payload(await game.SnapshotAsync("Amos")).Phase);
    }

    [Fact]
    public async Task Clue_payloads_of_the_wrong_shape_are_rejected()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var spymaster = t.Spymaster(t.Turn);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(spymaster, "clue")));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(spymaster, "clue", new { word = 4, count = 2 })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(spymaster, "clue", new { word = "ok", count = "2" })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(spymaster, "clue", new { word = new string('a', 100), count = 2 })));
    }

    [Fact]
    public async Task A_clue_cannot_be_a_word_still_on_the_board_however_it_is_written()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var onBoard = t.Views["Amos"].Board[0].Word;

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => ClueAsync(t, onBoard.ToUpperInvariant(), 2)));
    }

    [Fact]
    public async Task Guessing_before_a_clue_is_refused()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => GuessAsync(t, 0)));
    }

    [Fact]
    public async Task Only_the_operatives_of_the_team_in_play_can_guess_and_never_the_spymaster()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        await ClueAsync(t);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => GuessAsync(t, 0, t.Spymaster(t.Turn))));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => GuessAsync(t, 0, t.Operative(Team.Other(t.Turn)))));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => GuessAsync(t, 0, t.Spymaster(Team.Other(t.Turn)))));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task Guessing_a_card_off_the_board_is_rejected(int cell)
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        await ClueAsync(t);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => GuessAsync(t, cell)));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(t.Operative(t.Turn), "guess", new { cell = "1" })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(t.Operative(t.Turn), "guess")));
    }

    [Fact]
    public async Task A_correct_guess_keeps_the_turn_and_uses_up_one_guess()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "animals", 2);

        await GuessAsync(t, t.CellsOf(team).ElementAt(0));
        var view = Payload(await game.SnapshotAsync(t.Operative(team)));

        Assert.Equal(team, view.Turn);
        Assert.Equal(WordSpiesEngine.Guessing, view.Phase);
        Assert.Equal(2, view.GuessesLeft);
        Assert.Equal(8, view.Remaining[team]);
    }

    [Fact]
    public async Task The_turn_ends_when_the_allowed_guesses_run_out()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "animals", 1);

        await GuessAsync(t, t.CellsOf(team).ElementAt(0));
        await GuessAsync(t, t.CellsOf(team).ElementAt(1));
        var view = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(Team.Other(team), view.Turn);
        Assert.Equal(WordSpiesEngine.GivingClue, view.Phase);
        Assert.Null(view.Clue);
    }

    [Fact]
    public async Task A_neutral_card_ends_the_turn_and_a_rival_card_ends_it_and_helps_the_rival()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "animals", 5);
        await GuessAsync(t, t.CellsOf(Owner.Neutral).First());
        Assert.Equal(Team.Other(team), Payload(await game.SnapshotAsync("Amos")).Turn);

        // Now the other team is in play and turns over one of the first team's cards.
        await ClueAsync(t, "things", 5);
        await GuessAsync(t, t.CellsOf(team).First());
        var view = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(team, view.Turn);
        Assert.Equal(team, view.Board[t.CellsOf(team).First()].Owner);
        Assert.Equal(8, view.Remaining[team]);
    }

    [Fact]
    public async Task The_assassin_loses_the_game_for_the_team_that_turned_it_over_and_reveals_the_key_to_all()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t);

        await GuessAsync(t, t.CellsOf(Owner.Assassin).Single());
        var done = await ReadAsync(game);

        Assert.All(done.Views.Values, v =>
        {
            Assert.Equal(Phases.Complete, v.Phase);
            Assert.Equal(Team.Other(team), v.Winner);
            Assert.Equal("assassin", v.EndReason);
            Assert.NotNull(v.Key);
            Assert.All(v.Board, c => Assert.NotNull(c.Owner));
        });
        var scores = done.Views["Amos"].Scoreboard;
        Assert.All(scores, s => Assert.Equal(done.Views[s.Player].MyTeam == Team.Other(team) ? 1 : 0, s.Score));
    }

    [Fact]
    public async Task A_team_wins_by_finding_all_of_its_cards()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "everything", 9);

        foreach (var cell in t.CellsOf(team)) await GuessAsync(t, cell);
        var done = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Equal(team, done.Winner);
        Assert.Equal("all-found", done.EndReason);
    }

    [Fact]
    public async Task Turning_over_the_last_card_of_the_rival_hands_them_the_win()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        // Each turn, the team in play turns over a card of the other team, so the other team is the one that gets found out.
        while (Payload(await game.SnapshotAsync("Amos")).Phase != Phases.Complete)
        {
            var current = Payload(await game.SnapshotAsync("Amos")).Turn!;
            await ClueAsync(t, "hint", 1);
            var board = Payload(await game.SnapshotAsync("Amos")).Board;
            await GuessAsync(t, t.CellsOf(Team.Other(current)).First(c => board[c].Owner is null));
        }

        var done = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal("all-found", done.EndReason);
        Assert.NotNull(done.Winner);
        Assert.Equal(0, done.Remaining[done.Winner!]);
    }

    [Fact]
    public async Task A_card_cannot_be_turned_over_twice()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        await ClueAsync(t, "animals", 5);
        var cell = t.CellsOf(t.Turn).First();
        await GuessAsync(t, cell);

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => GuessAsync(t, cell)));
    }

    [Fact]
    public async Task Passing_needs_a_guess_first_and_then_hands_over_the_turn()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "animals", 3);

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(t.Operative(team), "pass")));
        await GuessAsync(t, t.CellsOf(team).First());
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(t.Operative(Team.Other(team)), "pass")));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(t.Spymaster(team), "pass")));
        await game.ActAsync(t.Operative(team), "pass");

        Assert.Equal(Team.Other(team), Payload(await game.SnapshotAsync("Amos")).Turn);
    }

    [Fact]
    public async Task Only_the_host_can_end_the_game_early_and_nothing_is_accepted_afterwards()
    {
        using var game = NewGame();
        var t = await StartAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "end")));
        var done = Payload(await game.ActAsync("Amos", "end"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Equal("ended", done.EndReason);
        Assert.Null(done.Winner);
        Assert.All(done.Scoreboard, s => Assert.Equal(0, s.Score));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(t.Spymaster(Team.Red), "clue", new { word = "animals", count = 2 })));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync(t.Operative(Team.Red), "guess", new { cell = 0 })));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "end")));
    }

    [Fact]
    public async Task Two_operatives_guessing_the_same_card_at_once_turn_it_over_only_once()
    {
        using var game = NewGame(players: ["Amos", "Lydia", "James", "Jacob", "Ruth", "Sam", "Tom", "Una"]);
        var t = await StartAsync(game);
        var team = t.Turn;
        await ClueAsync(t, "animals", 4);
        var cell = t.CellsOf(team).First();
        var first = t.OperativeOf(team, 0);
        var second = t.OperativeOf(team, 1);
        var other = game.Reopen();

        var results = await Task.WhenAll(Try(game, first, cell), Try(other, second, cell));
        other.Dispose();

        Assert.Single(results, ok => ok);
        var view = Payload(await game.SnapshotAsync("Amos"));
        Assert.Equal(1, view.Board.Count(c => c.Owner is not null));
        Assert.Equal(4, view.GuessesLeft);

        static async Task<bool> Try(GameHarness g, string p, int c)
        {
            try { await g.ActAsync(p, "guess", new { cell = c }); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    [Fact]
    public async Task The_key_teams_and_progress_survive_a_restart()
    {
        using var game = NewGame();
        var t = await StartAsync(game);
        await ClueAsync(t, "animals", 2);
        await GuessAsync(t, t.CellsOf(t.Turn).First());

        using var restarted = game.Reopen();
        var after = await ReadAsync(restarted);

        Assert.Equal(t.Key, after.Key);
        Assert.Equal(t.Views["Amos"].MyTeam, after.Views["Amos"].MyTeam);
        Assert.Equal(1, after.Views["Amos"].Board.Count(c => c.Owner is not null));
        Assert.Equal(2, after.Views.Values.Count(v => v.Key is not null));
    }

    [Fact]
    public void The_game_needs_at_least_four_players()
    {
        Assert.Throws<RoomRuleException>(() => NewGame(players: ["Amos", "Lydia", "James"]));
    }

    [Fact]
    public async Task Custom_words_join_the_pool_and_a_room_with_only_custom_words_uses_them_all()
    {
        var custom = Enumerable.Range(0, 30).Select(i => $"Custom{(char)('a' + i % 26)}{i}").ToArray();
        using var game = NewGame(new { words = custom, useBuiltIn = false });

        var view = Payload(await StartAndReadAsync(game));

        Assert.All(view.Board, c => Assert.StartsWith("Custom", c.Word));
    }

    private static async Task<RoomSnapshot> StartAndReadAsync(GameHarness game)
    {
        await game.StartAsync();
        return await game.SnapshotAsync("Amos");
    }

    [Theory]
    [InlineData("few")]
    [InlineData("dupes")]
    [InlineData("blank")]
    [InlineData("long")]
    [InlineData("many")]
    [InlineData("nontext")]
    public void Invalid_word_lists_are_rejected_at_the_boundary(string kind)
    {
        object setup = kind switch
        {
            "few" => new { words = new[] { "a", "b" }, useBuiltIn = false },
            "dupes" => new { words = new[] { "Same", " same " } },
            "blank" => new { words = new[] { "a", " " } },
            "long" => new { words = new[] { new string('x', WordSpiesEngine.MaxWordLength + 1) } },
            "many" => new { words = Enumerable.Range(0, WordSpiesEngine.MaxCustomWords + 1).Select(i => $"w{i}") },
            _ => new { words = new object[] { 1 } },
        };

        Assert.Throws<RoomRuleException>(() => NewGame(setup));
    }

    [Fact]
    public void The_built_in_bank_is_large_unique_and_free_of_multi_word_entries()
    {
        var bank = ContentBank.Load<string>("word-spies");

        Assert.True(bank.Count >= 100);
        Assert.Equal(bank.Count, bank.Select(AnswerNormalizer.Normalize).Distinct().Count());
        Assert.All(bank, w => Assert.InRange(w.Length, 2, WordSpiesEngine.MaxWordLength));
    }

    [Fact]
    public async Task An_unknown_action_is_rejected()
    {
        using var game = NewGame();
        await StartAsync(game);
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Amos", "peek")));
    }
}
