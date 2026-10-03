using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>
/// Oversized input must come back as a friendly InvalidInput (HTTP 400), never reach the database column limit
/// and surface as a 500.
/// </summary>
public class InputLimitTests
{
    private static GameHarness Picker(object? setup = null, string[]? players = null, string title = "Test room") =>
        new(d => new RandomPickerEngine(d.Db, d.Random, d.Clock), RandomPickerEngine.Key,
            setup ?? new { choices = new[] { "Sarah", "Judith" } }, players, title: title);

    private static void AssertRejected(Func<GameHarness> create, string expectedMessagePart)
    {
        var ex = Assert.Throws<RoomRuleException>(() => create());
        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
        Assert.Contains(expectedMessagePart, ex.Message);
    }

    [Fact]
    public void Player_name_longer_than_32_characters_is_rejected()
    {
        var tooLong = new string('a', 33);

        AssertRejected(() => Picker(players: ["Amos", tooLong]), "32");
    }

    [Fact]
    public void Player_name_of_exactly_32_characters_is_accepted()
    {
        using var h = Picker(players: ["Amos", new string('a', 32)]);

        Assert.NotEqual(Guid.Empty, h.RoomId);
    }

    [Fact]
    public void Room_title_longer_than_80_characters_is_rejected()
    {
        AssertRejected(() => Picker(title: new string('t', 81)), "80");
    }

    [Fact]
    public void Picker_choice_longer_than_80_characters_is_rejected()
    {
        AssertRejected(() => Picker(new { choices = new[] { "Sarah", new string('c', 81) } }), "80");
    }

    [Fact]
    public void Picker_with_more_than_50_choices_is_rejected()
    {
        var choices = Enumerable.Range(1, 51).Select(i => $"Choice {i}").ToArray();

        AssertRejected(() => Picker(new { choices }), "50");
    }

    [Fact]
    public void Trivia_question_longer_than_300_characters_is_rejected()
    {
        var question = new TestQuestion(new string('q', 301), ["Yes", "No"], 0);

        AssertRejected(() => new GameHarness(
            d => new RandomRoom.Api.Games.Trivia.TriviaEngine(d.Db, d.Clock), "trivia",
            new { questions = new[] { new { text = question.Text, options = question.Options, correctIndex = 0 } } }), "300");
    }

    [Fact]
    public void Values_exactly_at_each_limit_are_accepted()
    {
        var choices = Enumerable.Range(1, 49).Select(i => $"Choice {i}").Append(new string('c', 80)).ToArray();

        using var h = Picker(new { choices }, title: new string('t', 80));

        Assert.NotEqual(Guid.Empty, h.RoomId);
    }

    [Fact]
    public void Trivia_question_of_exactly_300_characters_is_accepted()
    {
        using var h = new GameHarness(
            d => new RandomRoom.Api.Games.Trivia.TriviaEngine(d.Db, d.Clock), "trivia",
            new { questions = new[] { new { text = new string('q', 300), options = new[] { "Yes", "No" }, correctIndex = 0 } } });

        Assert.NotEqual(Guid.Empty, h.RoomId);
    }

    // ---- trivia size limits (QUIZ-06) ----

    private static GameHarness Trivia(object setup) =>
        new(d => new RandomRoom.Api.Games.Trivia.TriviaEngine(d.Db, d.Clock), "trivia", setup);

    private static object Question(string text = "Q?", string[]? options = null) =>
        new { text, options = options ?? ["Yes", "No"], correctIndex = 0 };

    [Fact]
    public void Trivia_question_with_more_than_6_options_is_rejected()
    {
        var options = Enumerable.Range(1, 7).Select(i => $"Option {i}").ToArray();

        AssertRejected(() => Trivia(new { questions = new[] { Question(options: options) } }), "6 options");
    }

    [Fact]
    public void Trivia_option_longer_than_100_characters_is_rejected()
    {
        AssertRejected(() => Trivia(new { questions = new[] { Question(options: [new string('o', 101), "No"]) } }), "100");
    }

    [Fact]
    public void Trivia_with_more_than_50_questions_is_rejected()
    {
        var questions = Enumerable.Range(1, 51).Select(i => Question($"Question {i}?")).ToArray();

        AssertRejected(() => Trivia(new { questions }), "50 questions");
    }

    [Fact]
    public void Trivia_counts_the_starter_questions_towards_the_limit_of_50()
    {
        var questions = Enumerable.Range(1, 40).Select(i => Question($"Question {i}?")).ToArray();

        AssertRejected(() => Trivia(new { questions, useBuiltIn = true, builtInCount = 11 }), "starter");
    }

    [Fact]
    public void Trivia_values_exactly_at_the_new_limits_are_accepted()
    {
        var sixOptionsOfMaxLength = Enumerable.Range(0, 6).Select(i => new string((char)('a' + i), 100)).ToArray();
        var questions = Enumerable.Range(1, 49).Select(i => Question($"Question {i}?")).Append(Question(new string('q', 300), sixOptionsOfMaxLength)).ToArray();

        using var h = Trivia(new { questions });

        Assert.NotEqual(Guid.Empty, h.RoomId);
    }

    [Fact]
    public void Trivia_with_your_questions_and_the_starter_set_up_to_50_is_accepted()
    {
        var questions = Enumerable.Range(1, 30).Select(i => Question($"Question {i}?")).ToArray();

        using var h = Trivia(new { questions, useBuiltIn = true, builtInCount = 20 });

        Assert.NotEqual(Guid.Empty, h.RoomId);
    }
}
