using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.SpinWheel;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>A new game of the same kind gets its own copy of the previous game's setup, made with the session.</summary>
public class SessionSetupCopyTests
{
    private static readonly object TriviaSetup = new
    {
        questions = new[]
        {
            new { text = "2 + 2?", options = new[] { "3", "4" }, correctIndex = 1 },
            new { text = "Capital of Uganda?", options = new[] { "Kampala", "Gulu" }, correctIndex = 0 },
        },
        timeLimitSeconds = 20,
    };

    private static async Task<Guid> PlayOneAndStartNextAsync(GameHarness game)
    {
        await game.StartAsync();
        await game.EndAsync();
        await game.NewRoundAsync();
        return game.LatestSessionId;
    }

    [Fact]
    public async Task New_trivia_game_gets_a_copy_of_the_questions_and_time_limit()
    {
        using var game = new GameHarness(d => new TriviaEngine(d.Db, d.Clock, d.Random), TriviaEngine.Key, TriviaSetup);
        var first = game.LatestSessionId;

        var second = await PlayOneAndStartNextAsync(game);

        var questions = await game.Db.TriviaQuestions.AsNoTracking().Where(q => q.SessionId == second).OrderBy(q => q.Position).ToListAsync();
        Assert.Equal(["2 + 2?", "Capital of Uganda?"], questions.Select(q => q.Text));
        Assert.DoesNotContain(questions, q => game.Db.TriviaQuestions.Any(o => o.SessionId == first && o.Id == q.Id));
        Assert.Equal(1, await game.Db.RoomGameSetups.CountAsync(s => s.SessionId == second));
    }

    [Fact]
    public async Task New_random_picker_game_gets_a_copy_of_the_choices()
    {
        using var game = new GameHarness(d => new RandomPickerEngine(d.Db, d.Random, d.Clock), RandomPickerEngine.Key, new { choices = new[] { "Pizza", "Tacos" } });

        var second = await PlayOneAndStartNextAsync(game);

        Assert.Equal(["Pizza", "Tacos"], await game.Db.RoomChoices.Where(c => c.SessionId == second).OrderBy(c => c.Position).Select(c => c.Label).ToListAsync());
        Assert.Equal(RandomPickerEngine.Key, (await game.Db.GameSessions.SingleAsync(s => s.Id == second)).GameType);
    }

    [Fact]
    public async Task Snapshot_names_the_session_game()
    {
        using var game = new GameHarness(d => new RandomPickerEngine(d.Db, d.Random, d.Clock), RandomPickerEngine.Key, new { choices = new[] { "Pizza", "Tacos" } });

        var snapshot = await game.SnapshotAsync(game.Host);

        Assert.Equal(RandomPickerEngine.Key, snapshot.Session.GameType);
    }

    [Fact]
    public async Task Concurrent_start_new_game_makes_one_session_with_one_setup_copy()
    {
        using var game = new GameHarness(d => new SpinWheelEngine(d.Store, d.Random), SpinWheelEngine.Key, new { segments = new[] { "A", "B" } });
        await game.StartAsync();
        await game.EndAsync();
        var others = Enumerable.Range(0, 4).Select(_ => game.Reopen()).ToList();

        var results = await Task.WhenAll(others.Select(TryNewGameAsync));
        others.ForEach(o => o.Dispose());

        Assert.Equal(1, results.Count(started => started));
        Assert.Equal(2, await game.Db.GameSessions.CountAsync(s => s.RoomId == game.RoomId));
        Assert.Equal(2, await game.Db.RoomGameSetups.CountAsync(s => s.RoomId == game.RoomId));
        Assert.Equal(1, await game.Db.RoomGameSetups.CountAsync(s => s.SessionId == game.LatestSessionId));

        static async Task<bool> TryNewGameAsync(GameHarness g)
        {
            try
            {
                await g.NewRoundAsync();
                return true;
            }
            catch (RoomRuleException e) when (e.Violation == RuleViolation.Conflict)
            {
                return false;
            }
        }
    }
}
