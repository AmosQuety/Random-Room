using System.Text.Json;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games.GuessWho;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class GuessWhoTests
{
    private static GameHarness NewGame(string[]? players = null) =>
        new(d => new GuessWhoEngine(d.Store, d.Random), GuessWhoEngine.Key, new { }, players);

    private static GuessWhoPayload Payload(RoomSnapshot snapshot) => (GuessWhoPayload)snapshot.GamePayload;

    private static readonly string[] Facts = ["I have climbed a very tall tree", "I once met a famous singer", "I can speak three languages", "I have never been on a plane"];

    private static string FactOf(string player) => Facts[Array.IndexOf(RoomTestHarness.Players, player)];

    private static async Task SubmitAllAsync(GameHarness game)
    {
        foreach (var player in game.Players) await game.ActAsync(player, "submit", new { text = FactOf(player) });
    }

    private static string AuthorOf(GuessWhoPayload view, GameHarness game) =>
        game.Players.Single(p => view.Text == FactOf(p));

    [Fact]
    public async Task Facts_are_collected_then_play_begins_when_everyone_has_submitted()
    {
        using var game = NewGame();
        var started = Payload(await game.StartAsync());
        Assert.Equal(GuessWhoEngine.Submitting, started.Phase);

        foreach (var player in game.Players.Take(3)) await game.ActAsync(player, "submit", new { text = FactOf(player) });
        Assert.Equal(GuessWhoEngine.Submitting, Payload(await game.SnapshotAsync("Amos")).Phase);

        var view = Payload(await game.ActAsync(game.Players[3], "submit", new { text = FactOf(game.Players[3]) }));

        Assert.Equal(GuessWhoEngine.Voting, view.Phase);
        Assert.Equal(1, view.Round);
        Assert.Equal(4, view.TotalRounds);
        Assert.NotNull(view.Text);
    }

    [Fact]
    public async Task A_correct_guess_scores_and_the_author_is_revealed()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var view = Payload(await game.SnapshotAsync("Amos"));
        var author = AuthorOf(view, game);
        var guessers = game.Players.Where(p => p != author).ToArray();
        var wrong = guessers.First(g => g != guessers[0]);

        await game.ActAsync(guessers[0], "guess", new { player = author });
        await game.ActAsync(guessers[1], "guess", new { player = guessers[0] });
        var revealed = Payload(await game.ActAsync(guessers[2], "guess", new { player = author }));

        Assert.Equal(Phases.Revealed, revealed.Phase);
        Assert.Equal(author, revealed.Result!.Value.GetProperty("author").GetString());
        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == guessers[0]).Score);
        Assert.Equal(0, revealed.Scoreboard.Single(s => s.Player == guessers[1]).Score);
        Assert.Equal(1, revealed.Scoreboard.Single(s => s.Player == guessers[2]).Score);
        _ = wrong;
    }

    [Fact]
    public async Task Authorship_is_hidden_from_everyone_but_the_author_until_the_reveal()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var author = AuthorOf(Payload(await game.SnapshotAsync("Amos")), game);
        var other = game.Players.First(p => p != author);

        var authorView = Payload(await game.SnapshotAsync(author));
        var otherView = Payload(await game.SnapshotAsync(other));
        var publicView = Payload(await game.PublicSnapshotAsync());

        Assert.True(authorView.IsMine);
        Assert.False(otherView.IsMine);
        Assert.False(publicView.IsMine);
        foreach (var view in new[] { otherView, publicView })
        {
            Assert.Null(view.Result);
            Assert.DoesNotContain("author", JsonSerializer.Serialize(view, view.GetType(), JsonSerializerOptions.Web));
        }
    }

    [Fact]
    public async Task Nothing_per_player_singles_out_the_author_while_guessing()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var view = Payload(await game.SnapshotAsync("Amos"));
        var author = AuthorOf(view, game);
        var guesser = game.Players.First(p => p != author);
        await game.ActAsync(guesser, "guess", new { player = game.Players.First(p => p != guesser) });

        var after = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(game.Players.OrderBy(p => p), after.Submitted.Keys.OrderBy(p => p));
        Assert.All(after.Submitted, s => Assert.False(s.Value));
        Assert.Equal(game.Players.OrderBy(p => p), after.Scoreboard.Select(s => s.Player).OrderBy(p => p));
        Assert.Equal(1, after.GuessCount);
        Assert.Equal(3, after.GuessesNeeded);
    }

    [Fact]
    public async Task Other_players_facts_are_not_visible_while_facts_are_being_collected()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Lydia", "submit", new { text = FactOf("Lydia") });

        var amos = Payload(await game.SnapshotAsync("Amos"));
        var lydia = Payload(await game.SnapshotAsync("Lydia"));

        Assert.Null(amos.MyFact);
        Assert.Null(amos.Text);
        Assert.Equal(FactOf("Lydia"), lydia.MyFact);
        Assert.True(amos.Submitted["Lydia"]);
        Assert.DoesNotContain(FactOf("Lydia"), JsonSerializer.Serialize(amos, amos.GetType(), JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Who_submitted_is_hidden_once_play_begins_so_nobody_can_rule_players_out()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "submit", new { text = FactOf("Amos") });
        await game.ActAsync("Lydia", "submit", new { text = FactOf("Lydia") });
        await game.ActAsync(game.Host, "begin");

        var view = Payload(await game.SnapshotAsync("James"));

        Assert.Equal(GuessWhoEngine.Voting, view.Phase);
        Assert.All(view.Submitted, s => Assert.False(s.Value));
    }

    [Fact]
    public async Task The_author_cannot_guess_and_nobody_can_guess_themselves_or_a_stranger()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var author = AuthorOf(Payload(await game.SnapshotAsync("Amos")), game);
        var guesser = game.Players.First(p => p != author);

        var byAuthor = await GameHarness.RejectedAsync(() => game.ActAsync(author, "guess", new { player = guesser }));
        var self = await GameHarness.RejectedAsync(() => game.ActAsync(guesser, "guess", new { player = guesser }));
        var stranger = await GameHarness.RejectedAsync(() => game.ActAsync(guesser, "guess", new { player = "Mallory" }));

        Assert.Equal(RuleViolation.Forbidden, byAuthor.Violation);
        Assert.Equal(RuleViolation.InvalidInput, self.Violation);
        Assert.Equal(RuleViolation.InvalidInput, stranger.Violation);
    }

    [Fact]
    public async Task A_second_fact_or_a_second_guess_is_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "submit", new { text = "one" });
        var again = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "submit", new { text = "two" }));
        foreach (var p in game.Players.Skip(1)) await game.ActAsync(p, "submit", new { text = FactOf(p) });
        var view = Payload(await game.SnapshotAsync("Amos"));
        var author = view.IsMine ? "Amos" : game.Players.Single(p => p != "Amos" && FactOf(p) == view.Text);
        var guesser = game.Players.First(p => p != author);
        var other = game.Players.First(p => p != guesser);
        await game.ActAsync(guesser, "guess", new { player = other });

        var second = await GameHarness.RejectedAsync(() => game.ActAsync(guesser, "guess", new { player = author }));

        Assert.Equal(RuleViolation.Conflict, again.Violation);
        Assert.Equal(RuleViolation.Conflict, second.Violation);
    }

    [Theory]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("{\"text\":\"   \"}")]
    [InlineData("{\"text\":7}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task A_malformed_fact_is_invalid_input(string json)
    {
        using var game = NewGame();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "submit", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task An_oversized_fact_is_rejected()
    {
        using var game = NewGame();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "submit", new { text = new string('x', 201) }));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task Guessing_or_submitting_in_the_wrong_phase_is_a_conflict()
    {
        using var game = NewGame();
        await game.StartAsync();
        var early = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "guess", new { player = "Lydia" }));
        await SubmitAllAsync(game);
        var late = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "submit", new { text = "too late" }));

        Assert.Equal(RuleViolation.Conflict, early.Violation);
        Assert.Equal(RuleViolation.Conflict, late.Violation);
    }

    [Fact]
    public async Task Only_the_host_can_begin_reveal_or_move_on_and_begin_needs_two_facts()
    {
        using var game = NewGame();
        await game.StartAsync();
        var nonHost = game.Players.First(p => p != game.Host);

        var byPlayer = await GameHarness.RejectedAsync(() => game.ActAsync(nonHost, "begin"));
        var tooFew = await GameHarness.RejectedAsync(() => game.ActAsync(game.Host, "begin"));

        Assert.Equal(RuleViolation.Forbidden, byPlayer.Violation);
        Assert.Equal(RuleViolation.Conflict, tooFew.Violation);
    }

    [Fact]
    public async Task Every_fact_gets_a_round_and_the_session_completes_after_the_last()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var seen = new List<string>();

        for (var round = 1; round <= 4; round++)
        {
            var view = Payload(await game.SnapshotAsync("Amos"));
            seen.Add(AuthorOf(view, game));
            await game.ActAsync(game.Host, "reveal");
            var after = await game.ActAsync(game.Host, "next");
            Assert.Equal(round == 4 ? SessionStatus.Completed : SessionStatus.Active, after.Session.Status);
        }

        Assert.Equal(game.Players.OrderBy(p => p), seen.OrderBy(p => p));
    }

    [Fact]
    public async Task State_survives_a_restart()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var before = Payload(await game.SnapshotAsync("Amos"));

        using var restarted = game.Reopen();
        var after = Payload(await restarted.SnapshotAsync("Amos"));

        Assert.Equal(before.Text, after.Text);
        Assert.Equal(GuessWhoEngine.Voting, after.Phase);
    }

    [Fact]
    public async Task Simultaneous_final_guesses_reveal_exactly_once()
    {
        using var game = NewGame();
        await game.StartAsync();
        await SubmitAllAsync(game);
        var author = AuthorOf(Payload(await game.SnapshotAsync("Amos")), game);
        var guessers = game.Players.Where(p => p != author).ToArray();
        var contexts = guessers.Select(_ => game.Reopen()).ToList();

        try
        {
            await Task.WhenAll(guessers.Select((g, i) => contexts[i].ActAsync(g, "guess", new { player = author })));
            var view = Payload(await game.SnapshotAsync("Amos"));

            Assert.Equal(Phases.Revealed, view.Phase);
            Assert.Equal(3, view.Scoreboard.Sum(s => s.Score));
        }
        finally
        {
            contexts.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public void Two_players_are_not_enough()
    {
        var ex = Assert.Throws<RoomRuleException>(() => { using var g = NewGame(["Amos", "Lydia"]); });

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }
}
