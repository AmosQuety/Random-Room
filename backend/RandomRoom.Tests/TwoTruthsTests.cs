using System.Text.Json;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.TwoTruths;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class TwoTruthsTests
{
    private static readonly object Story = new { statements = new[] { "I have met a president", "I can juggle four balls", "I once ate a whole pie" }, lie = 1 };

    private static GameHarness NewGame(string[]? players = null) =>
        new(d => new TwoTruthsEngine(d.Store, d.Random), TwoTruthsEngine.Key, new { }, players);

    private static TwoTruthsPayload Payload(RoomSnapshot snapshot) => (TwoTruthsPayload)snapshot.GamePayload;

    private static async Task<(string Storyteller, string[] Voters)> StartAsync(GameHarness game)
    {
        var view = Payload(await game.StartAsync());
        return (view.Storyteller!, game.Players.Where(p => p != view.Storyteller).ToArray());
    }

    private static async Task VoteAllAsync(GameHarness game, string[] voters, int choice)
    {
        foreach (var voter in voters) await game.ActAsync(voter, "vote", new { choice });
    }

    [Fact]
    public async Task Starting_puts_one_player_up_as_storyteller_in_the_submitting_phase()
    {
        using var game = NewGame();

        var view = Payload(await game.StartAsync());

        Assert.Equal(TwoTruthsEngine.Submitting, view.Phase);
        Assert.Equal(1, view.Round);
        Assert.Equal(4, view.TotalRounds);
        Assert.Contains(view.Storyteller, game.Players);
        Assert.Null(view.Statements);
    }

    [Fact]
    public async Task A_full_round_scores_catchers_and_the_storyteller_for_each_player_fooled()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);

        await game.ActAsync(storyteller, "submit", Story);
        await game.ActAsync(voters[0], "vote", new { choice = 1 });
        await game.ActAsync(voters[1], "vote", new { choice = 0 });
        var view = Payload(await game.ActAsync(voters[2], "vote", new { choice = 2 }));

        Assert.Equal(Phases.Revealed, view.Phase);
        Assert.Equal(1, view.Scoreboard.Single(s => s.Player == voters[0]).Score);
        Assert.Equal(0, view.Scoreboard.Single(s => s.Player == voters[1]).Score);
        Assert.Equal(2, view.Scoreboard.Single(s => s.Player == storyteller).Score);
        Assert.Equal(1, view.Result!.Value.GetProperty("lie").GetInt32());
    }

    [Fact]
    public async Task Everyone_gets_exactly_one_turn_and_the_session_then_completes()
    {
        using var game = NewGame();
        await game.StartAsync();
        var seen = new List<string>();

        for (var round = 1; round <= 4; round++)
        {
            var storyteller = Payload(await game.SnapshotAsync("Amos")).Storyteller!;
            seen.Add(storyteller);
            await game.ActAsync(storyteller, "submit", Story);
            await VoteAllAsync(game, game.Players.Where(p => p != storyteller).ToArray(), 1);
            var after = await game.ActAsync("Amos", "next");
            Assert.Equal(round == 4 ? SessionStatus.Completed : SessionStatus.Active, after.Session.Status);
        }

        Assert.Equal(game.Players.OrderBy(p => p), seen.OrderBy(p => p));
    }

    [Fact]
    public async Task The_lie_never_reaches_anyone_but_the_storyteller_before_the_reveal()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);

        var storytellerView = Payload(await game.SnapshotAsync(storyteller));
        var voterView = Payload(await game.SnapshotAsync(voters[0]));
        var publicView = Payload(await game.PublicSnapshotAsync());

        Assert.Equal(1, storytellerView.MyLie);
        Assert.Null(voterView.MyLie);
        Assert.Null(publicView.MyLie);
        Assert.Null(voterView.Result);
        foreach (var view in new[] { voterView, publicView })
            Assert.DoesNotContain("\"lie\"", JsonSerializer.Serialize(view, view.GetType(), JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Statements_are_hidden_until_they_are_submitted_and_votes_stay_hidden_until_the_reveal()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        Assert.Null(Payload(await game.SnapshotAsync(voters[0])).Statements);
        await game.ActAsync(storyteller, "submit", Story);
        await game.ActAsync(voters[0], "vote", new { choice = 1 });

        var other = Payload(await game.SnapshotAsync(voters[1]));
        var voter = Payload(await game.SnapshotAsync(voters[0]));

        Assert.Equal(3, other.Statements!.Count);
        Assert.True(other.Voted[voters[0]]);
        Assert.Null(other.MyVote);
        Assert.Equal(1, voter.MyVote);
        Assert.Null(other.Result);
    }

    [Fact]
    public async Task Only_the_storyteller_can_submit()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync(voters[0], "submit", Story));

        Assert.Equal(RuleViolation.Forbidden, ex.Violation);
        await game.ActAsync(storyteller, "submit", Story);
    }

    [Fact]
    public async Task The_storyteller_cannot_vote_and_voters_cannot_vote_before_submission()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);

        var early = await GameHarness.RejectedAsync(() => game.ActAsync(voters[0], "vote", new { choice = 0 }));
        await game.ActAsync(storyteller, "submit", Story);
        var own = await GameHarness.RejectedAsync(() => game.ActAsync(storyteller, "vote", new { choice = 0 }));

        Assert.Equal(RuleViolation.Conflict, early.Violation);
        Assert.Equal(RuleViolation.Forbidden, own.Violation);
    }

    [Fact]
    public async Task A_second_vote_or_a_second_submission_is_rejected()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);
        await game.ActAsync(voters[0], "vote", new { choice = 0 });

        var vote = await GameHarness.RejectedAsync(() => game.ActAsync(voters[0], "vote", new { choice = 2 }));
        var submit = await GameHarness.RejectedAsync(() => game.ActAsync(storyteller, "submit", Story));

        Assert.Equal(RuleViolation.Conflict, vote.Violation);
        Assert.Equal(RuleViolation.Conflict, submit.Violation);
        Assert.Equal(0, Payload(await game.SnapshotAsync(voters[0])).MyVote);
    }

    public static IEnumerable<object[]> BadSubmissions =>
    [
        [new { statements = new[] { "a", "b" }, lie = 0 }],
        [new { statements = new[] { "a", "b", "c", "d" }, lie = 0 }],
        [new { statements = new[] { "a", "b", " " }, lie = 0 }],
        [new { statements = new[] { "a", "A!", "c" }, lie = 0 }],
        [new { statements = new[] { "a", "b", new string('x', 141) }, lie = 0 }],
        [new { statements = new[] { "a", "b", "c" }, lie = 3 }],
        [new { statements = new[] { "a", "b", "c" }, lie = -1 }],
        [new { statements = new[] { "a", "b", "c" } }],
        [new { lie = 0 }],
    ];

    [Theory]
    [MemberData(nameof(BadSubmissions))]
    public async Task Malformed_submissions_are_invalid_input(object body)
    {
        using var game = NewGame();
        var (storyteller, _) = await StartAsync(game);

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync(storyteller, "submit", body));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task A_vote_must_name_one_of_the_three_statements(int choice)
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync(voters[0], "vote", new { choice }));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task Only_the_host_can_reveal_next_or_skip()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        var nonHost = game.Players.First(p => p != game.Host);

        var skip = await GameHarness.RejectedAsync(() => game.ActAsync(nonHost, "skip"));
        await game.ActAsync(storyteller, "submit", Story);
        var reveal = await GameHarness.RejectedAsync(() => game.ActAsync(nonHost, "reveal"));

        Assert.Equal(RuleViolation.Forbidden, skip.Violation);
        Assert.Equal(RuleViolation.Forbidden, reveal.Violation);
        _ = voters;
    }

    [Fact]
    public async Task The_host_can_reveal_early_and_the_storyteller_scores_nothing_with_no_votes()
    {
        using var game = NewGame();
        var (storyteller, _) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);

        var view = Payload(await game.ActAsync(game.Host, "reveal"));

        Assert.Equal(Phases.Revealed, view.Phase);
        Assert.All(view.Scoreboard, s => Assert.Equal(0, s.Score));
    }

    [Fact]
    public async Task The_host_can_skip_a_storyteller_who_never_writes()
    {
        using var game = NewGame();
        var (first, _) = await StartAsync(game);

        var view = Payload(await game.ActAsync(game.Host, "skip"));

        Assert.Equal(2, view.Round);
        Assert.NotEqual(first, view.Storyteller);
        Assert.Equal(TwoTruthsEngine.Submitting, view.Phase);
    }

    [Fact]
    public async Task Next_before_the_reveal_and_skip_after_submission_are_rejected()
    {
        using var game = NewGame();
        var (storyteller, _) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);

        var next = await GameHarness.RejectedAsync(() => game.ActAsync(game.Host, "next"));
        var skip = await GameHarness.RejectedAsync(() => game.ActAsync(game.Host, "skip"));

        Assert.Equal(RuleViolation.Conflict, next.Violation);
        Assert.Equal(RuleViolation.Conflict, skip.Violation);
    }

    [Fact]
    public async Task An_unknown_action_is_invalid_input()
    {
        using var game = NewGame();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "cheat"));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task State_survives_a_restart_mid_round()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);
        await game.ActAsync(voters[0], "vote", new { choice = 2 });

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync(voters[0]));

        Assert.Equal(TwoTruthsEngine.Voting, view.Phase);
        Assert.Equal(storyteller, view.Storyteller);
        Assert.Equal(2, view.MyVote);
        Assert.Equal(3, view.Statements!.Count);
    }

    [Fact]
    public async Task Simultaneous_final_votes_reveal_exactly_once_and_score_once()
    {
        using var game = NewGame();
        var (storyteller, voters) = await StartAsync(game);
        await game.ActAsync(storyteller, "submit", Story);
        var contexts = voters.Select(_ => game.Reopen()).ToList();

        try
        {
            await Task.WhenAll(voters.Select((v, i) => contexts[i].ActAsync(v, "vote", new { choice = 0 })));
            var view = Payload(await game.SnapshotAsync("Amos"));

            Assert.Equal(Phases.Revealed, view.Phase);
            Assert.Equal(3, view.Scoreboard.Single(s => s.Player == storyteller).Score);
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
