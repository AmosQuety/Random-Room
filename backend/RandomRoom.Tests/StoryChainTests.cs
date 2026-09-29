using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.StoryChain;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class StoryChainTests
{
    private static GameHarness OneWord(object? setup = null, string[]? players = null) =>
        new(d => new StoryChainEngine(new OneWordRules(), d.Store, d.Random), "one-word-story", setup ?? new { useBuiltIn = true, length = 10 }, players);

    private static GameHarness Fortunately(object? setup = null, string[]? players = null) =>
        new(d => new StoryChainEngine(new FortunatelyRules(), d.Store, d.Random), "fortunately", setup ?? new { useBuiltIn = true, length = 4 }, players);

    private static ChainPayload Payload(RoomSnapshot snapshot) => (ChainPayload)snapshot.GamePayload;

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    private static async Task<string> WhoseTurnAsync(GameHarness game) => Payload(await game.SnapshotAsync("Amos")).CurrentPlayer!;

    // ---- shared turn rules (run against both games) ----

    public static TheoryData<string> Games => new() { "one-word-story", "fortunately" };

    private static GameHarness Build(string key, object? setup = null) => key == "one-word-story" ? OneWord(setup) : Fortunately(setup);

    private static string Contribution(string key, int i) => key == "one-word-story" ? $"word{i}" : $"something happens {i}";

    [Theory]
    [MemberData(nameof(Games))]
    public async Task The_story_starts_with_an_opener_and_the_first_player_to_write(string key)
    {
        using var game = Build(key);
        var view = Payload(await game.StartAsync());

        Assert.Equal(StoryChainEngine.Writing, view.Phase);
        Assert.False(string.IsNullOrWhiteSpace(view.Opener));
        Assert.Empty(view.Entries);
        Assert.Equal(0, view.Turn);
        Assert.NotNull(view.CurrentPlayer);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Only_the_player_whose_turn_it_is_can_add_and_turns_rotate_in_room_order(string key)
    {
        using var game = Build(key, key == "one-word-story" ? new { useBuiltIn = true, length = 12 } : new { useBuiltIn = true, length = 8 });
        await game.StartAsync();
        var order = Payload(await game.SnapshotAsync("Amos")).Scoreboard.Select(s => s.Player).ToList();

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(order[1], "add", new { text = Contribution(key, 0) })));
        for (var turn = 0; turn < order.Count * 2; turn++)
        {
            var expected = order[turn % order.Count];
            Assert.Equal(expected, await WhoseTurnAsync(game));
            await game.ActAsync(expected, "add", new { text = Contribution(key, turn) });
        }
        Assert.Equal(order.Count * 2, Payload(await game.SnapshotAsync("Amos")).Entries.Count);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task A_repeated_submission_from_the_same_player_is_refused(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        var first = await WhoseTurnAsync(game);
        await game.ActAsync(first, "add", new { text = Contribution(key, 0) });

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync(first, "add", new { text = Contribution(key, 1) })));
        Assert.Single(Payload(await game.SnapshotAsync("Amos")).Entries);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Simultaneous_submissions_for_one_turn_land_exactly_one_entry(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        var current = await WhoseTurnAsync(game);
        var other = game.Reopen();

        var results = await Task.WhenAll(Try(game, current, key), Try(other, current, key));
        other.Dispose();

        Assert.Single(results, ok => ok);
        Assert.Single(Payload(await game.SnapshotAsync("Amos")).Entries);

        static async Task<bool> Try(GameHarness g, string player, string k)
        {
            try { await g.ActAsync(player, "add", new { text = Contribution(k, 0) }); return true; }
            catch (RoomRuleException) { return false; }
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task The_story_completes_after_the_set_number_of_turns_and_accepts_nothing_more(string key)
    {
        using var game = Build(key, key == "one-word-story" ? new { useBuiltIn = true, length = 10 } : new { useBuiltIn = true, length = 4 });
        var view = Payload(await game.StartAsync());
        for (var i = 0; i < view.TotalTurns; i++) await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = Contribution(key, i) });

        var done = Payload(await game.SnapshotAsync("Amos"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Null(done.CurrentPlayer);
        Assert.Equal(view.TotalTurns, done.Entries.Count);
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "add", new { text = Contribution(key, 99) })));
        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Amos", "end")));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Only_the_host_can_skip_or_end_and_a_skip_uses_up_a_turn(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        var first = await WhoseTurnAsync(game);

        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "skip")));
        Assert.Equal(RuleViolation.Forbidden, await ViolationOf(() => game.ActAsync("Lydia", "end")));
        var view = Payload(await game.ActAsync("Amos", "skip"));

        Assert.Equal(1, view.Turn);
        Assert.Empty(view.Entries);
        Assert.NotEqual(first, view.CurrentPlayer);
        Assert.Equal(0, view.Scoreboard.Sum(s => s.Score));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task The_host_can_end_the_story_early(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = Contribution(key, 0) });

        var done = Payload(await game.ActAsync("Amos", "end"));

        Assert.Equal(Phases.Complete, done.Phase);
        Assert.Single(done.Entries);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Each_contribution_scores_a_point_for_its_author(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        var first = await WhoseTurnAsync(game);

        var view = Payload(await game.ActAsync(first, "add", new { text = Contribution(key, 0) }));

        Assert.Equal(1, view.Scoreboard.Single(s => s.Player == first).Score);
        Assert.Equal(1, view.Scoreboard.Sum(s => s.Score));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task Malformed_and_empty_contributions_are_rejected(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        var current = await WhoseTurnAsync(game);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add")));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text = "   " })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text = 5 })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text = new string('x', 500) })));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task The_story_survives_a_restart(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = Contribution(key, 0) });

        using var restarted = game.Reopen();
        var view = Payload(await restarted.SnapshotAsync("Jacob"));

        Assert.Single(view.Entries);
        Assert.Equal(1, view.Turn);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task An_unknown_action_is_rejected(string key)
    {
        using var game = Build(key);
        await game.StartAsync();
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Amos", "rewrite")));
    }

    [Theory]
    [InlineData("one-word-story", 9)]
    [InlineData("one-word-story", 101)]
    [InlineData("fortunately", 3)]
    [InlineData("fortunately", 31)]
    public void A_story_length_outside_the_allowed_range_is_rejected(string key, int length)
    {
        Assert.Throws<RoomRuleException>(() => Build(key, new { useBuiltIn = true, length }));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void Invalid_openers_are_rejected(string key)
    {
        Assert.Throws<RoomRuleException>(() => Build(key, new { openers = new[] { " " } }));
        Assert.Throws<RoomRuleException>(() => Build(key, new { openers = new[] { new string('x', StoryChainEngine.MaxOpenerLength + 1) } }));
        Assert.Throws<RoomRuleException>(() => Build(key, new { openers = new object[] { 3 } }));
        Assert.Throws<RoomRuleException>(() => Build(key, new { openers = Array.Empty<string>(), useBuiltIn = false }));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public async Task A_custom_opener_is_used(string key)
    {
        using var game = Build(key, new { openers = new[] { "My own opener" }, useBuiltIn = false });

        Assert.Equal("My own opener", Payload(await game.StartAsync()).Opener);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void The_built_in_openers_are_a_full_bank(string key)
    {
        var bank = ContentBank.Load<string>(key);

        Assert.Equal(30, bank.Count);
        Assert.All(bank, o => Assert.InRange(o.Length, 1, StoryChainEngine.MaxOpenerLength));
        Assert.Equal(bank.Count, bank.Distinct().Count());
    }

    // ---- One-word story ----

    [Theory]
    [InlineData("two words")]
    [InlineData("tab\tbed")]
    [InlineData("!!!")]
    [InlineData("supercalifragilisticexpialidocious")]
    public async Task One_word_story_takes_exactly_one_short_word(string text)
    {
        using var game = OneWord();
        await game.StartAsync();

        var current = await WhoseTurnAsync(game);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text })));
    }

    [Fact]
    public async Task One_word_story_keeps_punctuation_attached_to_the_word()
    {
        using var game = OneWord();
        await game.StartAsync();

        var view = Payload(await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = "  dragon! " }));

        Assert.Equal("dragon!", view.Entries[0].Text);
        Assert.Equal("", view.Prefix);
    }

    // ---- Fortunately / Unfortunately ----

    [Fact]
    public async Task Fortunately_alternates_its_lead_in_starting_with_fortunately()
    {
        using var game = Fortunately(new { useBuiltIn = true, length = 6 });
        var prefixes = new List<string>();
        await game.StartAsync();
        for (var i = 0; i < 4; i++)
        {
            var view = Payload(await game.SnapshotAsync("Amos"));
            prefixes.Add(view.Prefix!);
            await game.ActAsync(view.CurrentPlayer!, "add", new { text = $"a thing {i}" });
        }

        Assert.Equal(["Fortunately,", "Unfortunately,", "Fortunately,", "Unfortunately,"], prefixes);
    }

    [Theory]
    [InlineData("Fortunately, we had snacks", "we had snacks")]
    [InlineData("unfortunately the snacks ran out", "the snacks ran out")]
    [InlineData("we had snacks", "we had snacks")]
    public async Task Fortunately_removes_a_lead_in_the_player_typed_themselves(string typed, string stored)
    {
        using var game = Fortunately();
        await game.StartAsync();

        var view = Payload(await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = typed }));

        Assert.Equal(stored, view.Entries[0].Text);
    }

    [Fact]
    public async Task Each_entry_records_the_lead_in_of_its_own_turn_even_after_a_skip()
    {
        using var game = Fortunately(new { useBuiltIn = true, length = 6 });
        await game.StartAsync();
        await game.ActAsync("Amos", "skip");
        var view = Payload(await game.ActAsync(await WhoseTurnAsync(game), "add", new { text = "the snacks ran out" }));

        Assert.Equal("Unfortunately,", view.Entries[0].Prefix);
    }

    [Fact]
    public async Task Fortunately_rejects_a_lead_in_with_nothing_after_it_and_long_sentences()
    {
        using var game = Fortunately();
        await game.StartAsync();
        var current = await WhoseTurnAsync(game);

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text = "Fortunately," })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync(current, "add", new { text = new string('x', FortunatelyRules.MaxSentenceLength + 1) })));
    }

    [Fact]
    public async Task The_public_snapshot_matches_a_players_since_nothing_is_hidden()
    {
        using var game = Fortunately();
        await game.StartAsync();

        Assert.Equal(
            JsonSerializer.Serialize((await game.PublicSnapshotAsync()).GamePayload),
            JsonSerializer.Serialize((await game.SnapshotAsync("Lydia")).GamePayload));
    }
}
