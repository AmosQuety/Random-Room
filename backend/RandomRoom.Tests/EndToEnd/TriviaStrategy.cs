using System.Collections.Concurrent;
using System.Text.Json;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// Everyone answers the current question; the last answer moves the room on. The secret is the right option:
/// it may appear only once its question has closed.
/// </summary>
public sealed class TriviaStrategy(int questions = 4) : IGameStrategy
{
    private const int OptionCount = 3;

    private readonly ConcurrentDictionary<(string Player, int Question), int> picks = new();

    public string GameType => "trivia";

    public object Setup(int playerCount) => new
    {
        questions = Enumerable.Range(1, questions).Select(k => new
        {
            text = $"Question {k}?",
            options = Enumerable.Range(1, OptionCount).Select(o => $"Answer {o}"),
            correctIndex = CorrectIndexOf(k),
        }),
    };

    private static int CorrectIndexOf(int question) => question % OptionCount;

    public async Task<bool> PlayStepAsync(GameTable table)
    {
        var open = await table.Host.WaitForAsync(HasOpenQuestion, "a question to open");
        var question = open.Payload.GetProperty("questionNumber").GetInt32();
        await table.AllSeeAsync(s => HasOpenQuestion(s) && QuestionOf(s) == question, $"question {question} to open");

        await table.AllActAsync((bot, index) =>
        {
            var optionIndex = (index + question) % OptionCount;
            picks[(bot.Name, question)] = optionIndex;
            return bot.ActAsync("answer", new { optionIndex });
        });

        var isLast = question == open.Payload.GetProperty("totalQuestions").GetInt32();
        var next = await table.Host.WaitForAsync(s => isLast ? s.IsCompleted : HasOpenQuestion(s) && QuestionOf(s) == question + 1,
            $"question {question} to close");
        AssertRevealMatches(next, question);

        await table.AllSeeAsync(s => isLast ? s.IsCompleted : HasOpenQuestion(s) && QuestionOf(s) == question + 1, $"question {question} to close");
        if (isLast) AssertFinalScores(next);
        return isLast;
    }

    private void AssertRevealMatches(Snapshot closed, int question)
    {
        var reveal = closed.Payload.GetProperty("lastReveal");
        Assert.Equal(CorrectIndexOf(question), reveal.GetProperty("correctIndex").GetInt32());
        foreach (var answer in reveal.GetProperty("answers").EnumerateObject())
            Assert.Equal(picks[(answer.Name, question)], answer.Value.GetInt32());
    }

    private void AssertFinalScores(Snapshot finished)
    {
        foreach (var score in finished.Payload.GetProperty("scoreboard").EnumerateArray())
        {
            var player = score.GetProperty("player").GetString()!;
            var expected = Enumerable.Range(1, questions).Count(k => picks[(player, k)] == CorrectIndexOf(k));
            Assert.Equal(expected, score.GetProperty("correct").GetInt32());
        }
    }

    public IReadOnlyList<string> FindLeaks(GameTable table)
    {
        var leaks = new List<string>();
        foreach (var bot in table.Bots)
            foreach (var snapshot in bot.Received)
                leaks.AddRange(LeaksIn(bot.Name, snapshot));
        return leaks;
    }

    private static List<string> LeaksIn(string viewer, Snapshot snapshot)
    {
        var leaks = new List<string>();
        var payload = snapshot.Payload;
        var stillOpen = HasOpenQuestion(snapshot);
        var openNumber = payload.GetProperty("questionNumber").GetInt32();
        var where = $"{viewer}, sequence {snapshot.Sequence}";

        if (stillOpen && ContainsKey(payload.GetProperty("currentQuestion"), "correctIndex"))
            leaks.Add($"the open question carried its right option ({where})");

        if (payload.TryGetProperty("lastReveal", out var reveal) && reveal.ValueKind == JsonValueKind.Object)
        {
            var revealed = NumberIn(reveal.GetProperty("text").GetString()!);
            if (stillOpen && revealed >= openNumber)
                leaks.Add($"question {revealed}'s right option was sent while question {openNumber} was open ({where})");
        }

        foreach (var entry in payload.GetProperty("activity").EnumerateArray())
        {
            var number = entry.GetProperty("questionNumber").GetInt32();
            if (stillOpen && number >= openNumber)
                leaks.Add($"whether an answer to open question {number} was right was sent ({where})");
        }

        foreach (var (path, name, _) in JsonScan.Properties(payload))
            if (name == "correctIndex" && !path.StartsWith("lastReveal.", StringComparison.Ordinal))
                leaks.Add($"'{path}' appeared outside the reveal ({where})");

        return leaks;
    }

    private static bool ContainsKey(JsonElement element, string key) =>
        JsonScan.Properties(element).Any(p => p.Name == key);

    private static int NumberIn(string text) => int.Parse(new string(text.Where(char.IsDigit).ToArray()));

    private static bool HasOpenQuestion(Snapshot s) =>
        s.Payload.TryGetProperty("currentQuestion", out var q) && q.ValueKind == JsonValueKind.Object;

    private static int QuestionOf(Snapshot s) => s.Payload.GetProperty("questionNumber").GetInt32();
}
