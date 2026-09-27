using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>Binds GameSessionService calls to one fixed test trivia room.</summary>
public sealed class TriviaFacade(GameSessionService service, Guid roomId)
{
    public Task<RoomSnapshot> GetSnapshotAsync() => service.GetSnapshotAsync(roomId);
    public Task<RoomSnapshot> AnswerAsync(string player, int optionIndex) =>
        service.PerformActionAsync(roomId, player, "answer", JsonSerializer.SerializeToElement(new { optionIndex }));
    public Task<RoomSnapshot> StartRoundAsync(string actor) => service.StartSessionAsync(roomId, actor);
    public Task<RoomSnapshot> EndRoundAsync(string actor) => service.EndSessionAsync(roomId, actor);
    public Task<RoomSnapshot> StartNewRoundAsync(string actor) => service.StartNewSessionAsync(roomId, actor);
}

public sealed record TestQuestion(string Text, string[] Options, int CorrectIndex);

public sealed class TriviaTestHarness : IDisposable
{
    public static readonly string[] Players = ["Amos", "Lydia"];
    public const string HostPlayer = "Amos";

    public static readonly TestQuestion[] TwoQuestions =
    [
        new("2 + 2?", ["3", "4"], 1),
        new("Capital of France?", ["Paris", "Rome"], 0),
    ];

    private readonly TestDatabase database = new();

    public RoomDbContext Db { get; }
    public Guid RoomId { get; }
    public TriviaFacade Room { get; }

    public TriviaTestHarness(TestQuestion[]? questions = null)
    {
        questions ??= TwoQuestions;

        Db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        Db.Database.Migrate();

        IGameEngine[] engines = [new TriviaEngine(Db, TimeProvider.System)];
        var admin = new RoomAdminService(Db, TimeProvider.System, engines);
        var setup = new { questions = questions.Select(q => new { text = q.Text, options = q.Options, correctIndex = q.CorrectIndex }) };
        var created = admin.CreateRoomAsync(new CreateRoomRequest("Trivia room", TriviaEngine.Key, TestSetup.Of(setup), Players, HostPlayer))
            .GetAwaiter().GetResult();
        RoomId = Db.Rooms.Single(r => r.Slug == created.Slug).Id;

        var service = new GameSessionService(Db, new PresenceTracker(), TimeProvider.System, engines);
        Room = new TriviaFacade(service, RoomId);
    }

    public void Dispose()
    {
        Db.Dispose();
        database.Dispose();
    }
}

public class TriviaEngineTests
{
    private static TriviaPayload Payload(RoomSnapshot snapshot) => (TriviaPayload)snapshot.GamePayload;

    [Fact]
    public async Task New_session_shows_the_first_question_once_started()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);

        var payload = Payload(await h.Room.GetSnapshotAsync());

        Assert.Equal("2 + 2?", payload.CurrentQuestion!.Text);
        Assert.Equal(1, payload.QuestionNumber);
        Assert.Equal(2, payload.TotalQuestions);
        Assert.All(payload.Answered.Values, Assert.False);
        Assert.Null(payload.LastReveal);
    }

    [Fact]
    public async Task Session_advances_once_everyone_answers_and_reveals_the_previous_question()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);

        await h.Room.AnswerAsync("Amos", 1); // correct
        var snapshot = await h.Room.AnswerAsync("Lydia", 0); // incorrect
        var payload = Payload(snapshot);

        Assert.Equal("Capital of France?", payload.CurrentQuestion!.Text);
        Assert.Equal(2, payload.QuestionNumber);
        Assert.NotNull(payload.LastReveal);
        Assert.Equal("2 + 2?", payload.LastReveal!.Text);
        Assert.Equal(1, payload.LastReveal.CorrectIndex);
        Assert.Equal(1, payload.LastReveal.Answers["Amos"]);
        Assert.Equal(1, payload.Scoreboard.Single(s => s.Player == "Amos").Correct);
        Assert.Equal(0, payload.Scoreboard.Single(s => s.Player == "Lydia").Correct);
    }

    [Fact]
    public async Task Session_completes_after_the_last_question_is_answered_by_everyone()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);

        await h.Room.AnswerAsync("Amos", 1); // correct
        await h.Room.AnswerAsync("Lydia", 0); // incorrect
        await h.Room.AnswerAsync("Amos", 0); // correct
        var snapshot = await h.Room.AnswerAsync("Lydia", 1); // incorrect

        Assert.Equal(SessionStatus.Completed, snapshot.Session.Status);
        var payload = Payload(snapshot);
        Assert.Null(payload.CurrentQuestion);
        Assert.Equal(2, payload.Scoreboard.Single(s => s.Player == "Amos").Correct);
        Assert.Equal(0, payload.Scoreboard.Single(s => s.Player == "Lydia").Correct);
    }

    [Fact]
    public async Task Player_cannot_answer_the_same_question_twice()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);
        await h.Room.AnswerAsync("Amos", 1);

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.AnswerAsync("Amos", 0));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public async Task Out_of_range_option_is_rejected()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.AnswerAsync("Amos", 7));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }

    [Fact]
    public async Task Only_the_host_controls_the_session()
    {
        using var h = new TriviaTestHarness();

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.StartRoundAsync("Lydia"));

        Assert.Equal(RuleViolation.Forbidden, ex.Violation);
    }

    [Fact]
    public async Task Answers_cannot_be_modified_once_recorded()
    {
        using var h = new TriviaTestHarness();
        await h.Room.StartRoundAsync(TriviaTestHarness.HostPlayer);
        await h.Room.AnswerAsync("Amos", 1);

        var recorded = h.Db.TriviaAnswers.Single();
        recorded.OptionIndex = 0;

        Assert.Throws<InvalidOperationException>(() => h.Db.SaveChanges());
    }

    [Fact]
    public void Configuring_a_question_with_a_bad_correct_index_is_rejected()
    {
        var badQuestion = new TestQuestion("2 + 2?", ["3", "4"], 5);

        var ex = Assert.Throws<RoomRuleException>(() => new TriviaTestHarness([badQuestion]));

        Assert.Equal(RuleViolation.InvalidInput, ex.Violation);
    }
}
