using System.Text.Json;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class MadLibsTests
{
    private const string Story = "The {adjective} {animal} ate {number} {plural noun} and went to the {place}.";

    private static object Template(string text = Story, string title = "Test story") => new { title, text };

    private static GameHarness NewGame(object? setup = null, string[]? players = null) =>
        new(d => new RoundGameEngine<MadLibPrompt>(new MadLibsRules(), d.Store, d.Random, d.Clock), "mad-libs",
            setup ?? new { prompts = new[] { Template() }, rounds = 1 }, players);

    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    private static JsonElement PromptOf(RoundPayload view) => JsonSerializer.SerializeToElement(view.Prompt, JsonSerializerOptions.Web);

    private static int[] AssignedTo(RoundPayload view, string player) =>
        PromptOf(view).GetProperty("assigned").GetProperty(player).EnumerateArray().Select(e => e.GetInt32()).ToArray();

    private static object WordsFor(RoundPayload view, string player) =>
        new { words = AssignedTo(view, player).Select(i => $"w{i}").ToArray() };

    private static async Task<RoundPayload> AnswerAllAsync(GameHarness game, RoundPayload view)
    {
        RoundPayload last = view;
        foreach (var player in game.Players) last = Payload(await game.ActAsync(player, "answer", WordsFor(view, player)));
        return last;
    }

    private static async Task<RuleViolation> ViolationOf(Func<Task> act) => (await GameHarness.RejectedAsync(act)).Violation;

    [Fact]
    public async Task Blanks_are_shared_out_so_every_blank_is_asked_of_someone()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());

        var asked = game.Players.SelectMany(p => AssignedTo(view, p)).ToList();

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }.Order(), asked.Distinct().Order());
        Assert.Equal(5, PromptOf(view).GetProperty("labels").GetArrayLength());
    }

    [Fact]
    public async Task Players_see_word_types_but_never_the_story_until_the_reveal()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());
        var json = JsonSerializer.Serialize(view.Prompt, JsonSerializerOptions.Web);

        Assert.Contains("adjective", json);
        Assert.DoesNotContain("ate", json);
        Assert.DoesNotContain("went to the", json);
        var publicJson = JsonSerializer.Serialize((await game.PublicSnapshotAsync()).GamePayload, JsonSerializerOptions.Web);
        Assert.DoesNotContain("went to the", publicJson);
    }

    [Fact]
    public async Task When_everyone_has_answered_the_finished_story_is_revealed_with_each_word_credited()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());

        var done = await AnswerAllAsync(game, view);

        Assert.Equal(Phases.Revealed, done.Phase);
        var parts = done.Result!.Value.GetProperty("parts").EnumerateArray().ToList();
        var words = parts.Where(p => p.TryGetProperty("word", out _)).ToList();
        Assert.Equal(5, words.Count);
        for (var i = 0; i < words.Count; i++) Assert.Equal($"w{i}", words[i].GetProperty("word").GetString());
        Assert.All(words, w => Assert.Contains(w.GetProperty("by").GetString()!, game.Players));
        Assert.All(done.Scoreboard, s => Assert.Equal(1, s.Score));
    }

    [Fact]
    public async Task Wrong_number_of_words_is_rejected()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());
        var mine = AssignedTo(view, "Lydia").Length;

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer", new { words = new string[mine + 1].Select(_ => "x").ToArray() })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer", new { words = Array.Empty<string>() })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer")));
    }

    [Fact]
    public async Task Blank_oversized_and_non_text_words_are_rejected()
    {
        using var game = NewGame(new { prompts = new[] { Template("A {noun} and a {verb} and a {place}.") }, rounds = 1 }, ["Amos", "Lydia", "James"]);
        var view = Payload(await game.StartAsync());
        Assert.Single(AssignedTo(view, "Lydia"));

        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer", new { words = new[] { "  " } })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer", new { words = new[] { new string('x', MadLibsRules.MaxWordLength + 1) } })));
        Assert.Equal(RuleViolation.InvalidInput, await ViolationOf(() => game.ActAsync("Lydia", "answer", new { words = new object[] { 5 } })));
    }

    [Fact]
    public async Task A_player_cannot_answer_twice()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());
        await game.ActAsync("Lydia", "answer", WordsFor(view, "Lydia"));

        Assert.Equal(RuleViolation.Conflict, await ViolationOf(() => game.ActAsync("Lydia", "answer", WordsFor(view, "Lydia"))));
    }

    [Fact]
    public async Task Words_are_shown_as_plain_text_and_kept_as_typed()
    {
        using var game = NewGame(new { prompts = new[] { Template("A {noun} and a {verb}.") }, rounds = 1 }, ["Amos", "Lydia"]);
        await game.StartAsync();
        await game.ActAsync("Amos", "answer", new { words = new[] { "<b>bold</b>" } });
        var result = Payload(await game.ActAsync("Lydia", "answer", new { words = new[] { "run" } })).Result!.Value;
        Assert.Contains(result.GetProperty("parts").EnumerateArray(), p => p.TryGetProperty("word", out var w) && w.GetString() == "<b>bold</b>");
    }

    [Fact]
    public async Task With_fewer_blanks_than_players_each_player_still_has_a_blank_and_the_first_asked_supplies_it()
    {
        using var game = NewGame(new { prompts = new[] { Template("A {noun} and a {verb}.") }, rounds = 1 });
        var view = Payload(await game.StartAsync());
        foreach (var player in game.Players) Assert.Single(AssignedTo(view, player));

        foreach (var player in game.Players) await game.ActAsync(player, "answer", new { words = new[] { player } });
        var parts = Payload(await game.SnapshotAsync("Amos")).Result!.Value.GetProperty("parts").EnumerateArray()
            .Where(p => p.TryGetProperty("word", out _)).ToList();

        Assert.All(parts, p => Assert.Equal(p.GetProperty("word").GetString(), p.GetProperty("by").GetString()));
    }

    [Fact]
    public async Task A_blank_nobody_filled_is_shown_as_dots_when_the_host_reveals_early()
    {
        using var game = NewGame();
        await game.StartAsync();

        var view = Payload(await game.ActAsync("Amos", "reveal"));

        var words = view.Result!.Value.GetProperty("parts").EnumerateArray().Where(p => p.TryGetProperty("word", out _)).ToList();
        Assert.All(words, w => Assert.Equal("...", w.GetProperty("word").GetString()));
    }

    [Fact]
    public async Task Built_in_stories_are_valid_and_available()
    {
        using var game = NewGame(new { useBuiltIn = true, rounds = 30 });
        var view = Payload(await game.StartAsync());

        Assert.Equal(30, view.TotalRounds);
        var stories = ContentBank.Load<MadLibPrompt>("mad-libs");
        Assert.Equal(30, stories.Count);
        Assert.All(stories, s =>
        {
            var blanks = MadLibsRules.Labels(s.Text);
            Assert.InRange(blanks.Count, MadLibsRules.MinBlanks, MadLibsRules.MaxBlanks);
            Assert.All(blanks, l => Assert.InRange(l.Length, 1, MadLibsRules.MaxLabelLength));
        });
    }

    [Theory]
    [InlineData("one blank", "Just a {noun} here.")]
    [InlineData("none", "No blanks at all.")]
    [InlineData("empty label", "A {} and a {noun}.")]
    [InlineData("long label", "A {this label is far too long to keep} and a {noun}.")]
    [InlineData("too many", "{a}{b}{c}{d}{e}{f}{g}{h}{i}{j}{k}{l}{m}")]
    public void Invalid_stories_are_rejected_at_the_boundary(string _, string text)
    {
        Assert.Throws<RoomRuleException>(() => NewGame(new { prompts = new[] { Template(text) } }));
    }

    [Fact]
    public void A_story_without_a_title_or_too_long_is_rejected()
    {
        Assert.Throws<RoomRuleException>(() => NewGame(new { prompts = new object[] { new { text = Story } } }));
        Assert.Throws<RoomRuleException>(() => NewGame(new { prompts = new[] { Template(new string('x', MadLibsRules.MaxTextLength) + "{a}{b}") } }));
    }

    [Fact]
    public async Task Answers_survive_a_restart()
    {
        using var game = NewGame();
        var view = Payload(await game.StartAsync());
        await game.ActAsync("Lydia", "answer", WordsFor(view, "Lydia"));

        using var restarted = game.Reopen();
        var after = Payload(await restarted.SnapshotAsync("Lydia"));

        Assert.True(after.Answered["Lydia"]);
        Assert.NotNull(after.MyAnswer);
    }
}
