using System.Text.Json;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class NameThatTests
{
    private static object Clue(string clue = "A yellow submarine and four lads", string? link = null, string[]? answers = null) =>
        link is null
            ? new { clue, answers = answers ?? ["Yellow Submarine", "The Yellow Submarine"] }
            : new { clue, link, answers = answers ?? ["Yellow Submarine"] };

    private static GameHarness NewGame(object? setup = null, string[]? players = null) =>
        new(d => new RoundGameEngine<IntroPrompt>(new NameThatRules(), d.Store, d.Random, d.Clock), "name-that",
            setup ?? new { prompts = new[] { Clue() }, rounds = 1 }, players);

    private static RoundPayload Payload(RoomSnapshot snapshot) => (RoundPayload)snapshot.GamePayload;

    private static RoomRuleException SetupRejected(object prompt) =>
        Assert.Throws<RoomRuleException>(() => { using var g = NewGame(new { prompts = new[] { prompt } }); });

    [Fact]
    public async Task Guesses_are_marked_by_the_server_and_the_first_correct_one_earns_a_bonus()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Lydia", "answer", new { text = "  the YELLOW submarine! " });
        await game.ActAsync("Amos", "answer", new { text = "Yellow Submarine" });
        await game.ActAsync("James", "answer", new { text = "Penny Lane" });
        var view = Payload(await game.ActAsync("Jacob", "answer", new { text = "Help" }));

        Assert.Equal(2, view.Scoreboard.Single(s => s.Player == "Lydia").Score);
        Assert.Equal(1, view.Scoreboard.Single(s => s.Player == "Amos").Score);
        Assert.Equal(0, view.Scoreboard.Single(s => s.Player == "James").Score);
        Assert.Equal("Lydia", view.Result!.Value.GetProperty("first").GetString());
    }

    [Fact]
    public async Task Nobody_correct_means_nobody_scores()
    {
        using var game = NewGame();
        await game.StartAsync();
        await game.ActAsync("Amos", "reveal");

        var view = Payload(await game.SnapshotAsync("Amos"));

        Assert.All(view.Scoreboard, s => Assert.Equal(0, s.Score));
    }

    [Fact]
    public async Task The_accepted_answers_stay_secret_until_the_reveal()
    {
        using var game = NewGame();
        var collecting = Payload(await game.StartAsync());
        var json = JsonSerializer.Serialize(collecting, collecting.GetType(), JsonSerializerOptions.Web);

        Assert.DoesNotContain("Yellow Submarine", json);
        Assert.DoesNotContain("accepted", json);

        var revealed = Payload(await game.ActAsync("Amos", "reveal"));
        Assert.Equal(2, revealed.Result!.Value.GetProperty("accepted").GetArrayLength());
    }

    [Fact]
    public async Task Players_get_the_clue_and_the_link_to_open()
    {
        using var game = NewGame(new { prompts = new[] { Clue(link: "https://example.com/clip") }, rounds = 1 });

        var view = Payload(await game.StartAsync());
        var prompt = JsonSerializer.SerializeToElement(view.Prompt, JsonSerializerOptions.Web);

        Assert.Equal("https://example.com/clip", prompt.GetProperty("link").GetString());
        Assert.Equal("A yellow submarine and four lads", prompt.GetProperty("clue").GetString());
    }

    [Theory]
    [InlineData("http://example.com/clip")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/clip")]
    [InlineData("//example.com/clip")]
    [InlineData("example.com/clip")]
    [InlineData("https://user:pass@example.com/clip")]
    [InlineData("not a link")]
    public void Only_plain_https_links_are_accepted(string link) =>
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(Clue(link: link)).Violation);

    [Fact]
    public void A_link_is_optional()
    {
        using var game = NewGame(new { prompts = new[] { Clue() } });

        Assert.NotNull(game);
    }

    [Fact]
    public void A_clue_needs_text_and_at_least_one_accepted_answer()
    {
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = " ", answers = new[] { "x" } }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c", answers = Array.Empty<string>() }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c", answers = new[] { " ", "" } }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c", answers = new[] { "!!!" } }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c" }).Violation);
    }

    [Fact]
    public void Oversized_fields_are_rejected()
    {
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = new string('x', 201), answers = new[] { "x" } }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c", answers = new[] { new string('x', 81) } }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(new { clue = "c", answers = Enumerable.Range(0, 7).Select(i => $"a{i}").ToArray() }).Violation);
        Assert.Equal(RuleViolation.InvalidInput, SetupRejected(Clue(link: "https://example.com/" + new string('x', 300))).Violation);
    }

    [Fact]
    public void A_room_needs_at_least_one_clue_because_there_is_no_built_in_set()
    {
        var ex = Assert.Throws<RoomRuleException>(() => { using var g = NewGame(new { useBuiltIn = true }); });

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Theory]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("{\"text\":5}")]
    [InlineData("{}")]
    public async Task A_malformed_guess_is_invalid_input(string json)
    {
        using var game = NewGame();
        await game.StartAsync();

        var ex = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", JsonDocument.Parse(json).RootElement));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task An_oversized_guess_is_rejected_and_a_second_guess_is_a_conflict()
    {
        using var game = NewGame();
        await game.StartAsync();

        var big = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { text = new string('x', 81) }));
        await game.ActAsync("Amos", "answer", new { text = "Help" });
        var again = await GameHarness.RejectedAsync(() => game.ActAsync("Amos", "answer", new { text = "Yellow Submarine" }));

        Assert.Equal(RuleViolation.InvalidInput, big.Violation);
        Assert.Equal(RuleViolation.Conflict, again.Violation);
    }
}
