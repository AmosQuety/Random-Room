using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RandomRoom.Api.Data;

namespace RandomRoom.Tests;

/// <summary>
/// The move of game type and setup from the room to the session, run on a database that already holds rooms with
/// several sessions of every kind of setup, then rolled back and applied again.
/// </summary>
public sealed class SessionOwnsGameMigrationTests : IAsyncLifetime
{
    private const string Previous = "20261031175246_AddGameSessionHostPlays";

    private readonly TestDatabase database = new();
    private RoomDbContext db = null!;
    private IMigrator migrator = null!;

    private readonly Guid picker = Guid.NewGuid(), trivia = Guid.NewGuid(), wheel = Guid.NewGuid(),
        forbidden = Guid.NewGuid(), twoTruths = Guid.NewGuid(), sessionless = Guid.NewGuid();

    private readonly Dictionary<Guid, List<Guid>> sessions = [];
    private readonly List<Guid> pickerChoices = [], triviaQuestions = [];
    private Guid triviaOptions, wheelSetup, forbiddenSetup;

    private const string YesNo = "[\"Yes\",\"No\"]";

    public async Task InitializeAsync()
    {
        db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        database.Dispose();
    }

    private async Task SeedAsync()
    {
        await AddRoomAsync(picker, "picker", "random-picker", sessionCount: 3);
        await AddRoomAsync(trivia, "trivia", "trivia", sessionCount: 2);
        await AddRoomAsync(wheel, "wheel", "spin-wheel", sessionCount: 2);
        await AddRoomAsync(forbidden, "forbidden", "forbidden-words", sessionCount: 3);
        await AddRoomAsync(twoTruths, "truths", "two-truths", sessionCount: 2);
        await AddRoomAsync(sessionless, "nosession", "spin-wheel", sessionCount: 0);

        foreach (var (label, position) in new[] { ("Pizza", 0), ("Tacos", 1), ("Sushi", 2) })
            pickerChoices.Add(await InsertAsync(id =>
                $"INSERT INTO \"RoomChoices\" (\"Id\", \"RoomId\", \"Label\", \"Position\") VALUES ({id}, {picker}, {label}, {position})"));

        foreach (var position in Enumerable.Range(0, 3))
            triviaQuestions.Add(await InsertAsync(id =>
                $"INSERT INTO \"TriviaQuestions\" (\"Id\", \"RoomId\", \"Position\", \"Text\", \"Options\", \"CorrectIndex\", \"Category\") VALUES ({id}, {trivia}, {position}, {"Question " + position}, {YesNo}, 0, 'Science')"));
        triviaOptions = await AddSetupAsync(trivia, "{\"timeLimitSeconds\": 20}");
        wheelSetup = await AddSetupAsync(wheel, "{\"segments\": [\"A\", \"B\"]}");
        forbiddenSetup = await AddSetupAsync(forbidden, "{\"rounds\": 4}");
        await AddSetupAsync(sessionless, "{\"segments\": [\"C\"]}");

        // Both trivia sessions answered questions 0 and 1; scores depend on these pointing at the session's own questions.
        foreach (var session in sessions[trivia])
            foreach (var (question, option) in new[] { (triviaQuestions[0], 0), (triviaQuestions[1], 1) })
                await InsertAsync(id =>
                    $"INSERT INTO \"TriviaAnswers\" (\"Id\", \"SessionId\", \"QuestionId\", \"TriggeredBy\", \"OptionIndex\", \"Timestamp\") VALUES ({id}, {session}, {question}, 'Amos', {option}, now())");
    }

    private async Task AddRoomAsync(Guid roomId, string slug, string gameType, int sessionCount)
    {
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO \"Rooms\" (\"Id\", \"Slug\", \"Title\", \"GameType\", \"HostPlayer\", \"CreatedAt\") VALUES ({roomId}, {slug}, 'Room', {gameType}, 'Amos', now())");
        sessions[roomId] = [];
        for (var number = 1; number <= sessionCount; number++)
        {
            var status = number == sessionCount ? "Active" : "Completed";
            sessions[roomId].Add(await InsertAsync(id =>
                $"INSERT INTO \"GameSessions\" (\"Id\", \"RoomId\", \"Number\", \"Status\", \"CreatedAt\") VALUES ({id}, {roomId}, {number}, {status}, now())"));
        }
    }

    private Task<Guid> AddSetupAsync(Guid roomId, string json) =>
        InsertAsync(id => $"INSERT INTO \"RoomGameSetups\" (\"Id\", \"RoomId\", \"Json\") VALUES ({id}, {roomId}, {json}::jsonb)");

    private async Task<Guid> InsertAsync(Func<Guid, FormattableString> sql)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync(sql(id));
        return id;
    }

    private Guid Latest(Guid roomId) => sessions[roomId][^1];

    [Fact]
    public async Task Every_session_gets_its_room_game_type()
    {
        await migrator.MigrateAsync();

        var mismatched = await db.GameSessions.AsNoTracking()
            .Where(s => db.Rooms.Any(r => r.Id == s.RoomId && r.GameType != s.GameType)).CountAsync();
        Assert.Equal(0, mismatched);
        Assert.Equal(12, await db.GameSessions.CountAsync(s => s.GameType != ""));
    }

    [Fact]
    public async Task After_the_move_a_session_without_a_game_type_is_refused()
    {
        await migrator.MigrateAsync();

        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO \"GameSessions\" (\"Id\", \"RoomId\", \"Number\", \"Status\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {picker}, 9, 'Waiting', now())"));
        Assert.Equal("23502", ex.SqlState);
    }

    [Fact]
    public async Task Every_session_gets_a_copy_of_its_room_choices_and_the_latest_keeps_the_originals()
    {
        await migrator.MigrateAsync();

        foreach (var session in sessions[picker])
        {
            var labels = await db.RoomChoices.AsNoTracking().Where(c => c.SessionId == session).OrderBy(c => c.Position).Select(c => c.Label).ToListAsync();
            Assert.Equal(["Pizza", "Tacos", "Sushi"], labels);
        }
        var latestIds = await db.RoomChoices.AsNoTracking().Where(c => c.SessionId == Latest(picker)).Select(c => c.Id).ToListAsync();
        Assert.Equal(pickerChoices.Order(), latestIds.Order());
        Assert.Equal(9, await db.RoomChoices.CountAsync(c => c.RoomId == picker));
    }

    [Fact]
    public async Task Every_session_gets_one_copy_of_its_room_setup_document()
    {
        await migrator.MigrateAsync();

        foreach (var (room, original) in new[] { (trivia, triviaOptions), (wheel, wheelSetup), (forbidden, forbiddenSetup) })
        {
            var rows = await db.RoomGameSetups.AsNoTracking().Where(s => s.RoomId == room).ToListAsync();
            Assert.Equal(sessions[room].Order(), rows.Select(r => r.SessionId!.Value).Order());
            Assert.Single(rows.Select(r => r.Json).Distinct());
            Assert.Equal(original, rows.Single(r => r.SessionId == Latest(room)).Id);
        }
    }

    [Fact]
    public async Task Trivia_questions_are_copied_and_each_answer_points_at_its_own_session_copy()
    {
        await migrator.MigrateAsync();

        foreach (var session in sessions[trivia])
        {
            var questions = await db.TriviaQuestions.AsNoTracking().Where(q => q.SessionId == session).ToListAsync();
            Assert.Equal([0, 1, 2], questions.Select(q => q.Position).Order());
            var answers = await db.TriviaAnswers.AsNoTracking().Where(a => a.SessionId == session).ToListAsync();
            Assert.Equal(2, answers.Count);
            Assert.All(answers, a => Assert.Contains(a.QuestionId, questions.Select(q => q.Id)));
            Assert.Equal([(0, 0), (1, 1)], answers.Select(a => (questions.Single(q => q.Id == a.QuestionId).Position, a.OptionIndex)).Order());
        }
        var latestIds = await db.TriviaQuestions.AsNoTracking().Where(q => q.SessionId == Latest(trivia)).Select(q => q.Id).ToListAsync();
        Assert.Equal(triviaQuestions.Order(), latestIds.Order());
    }

    [Fact]
    public async Task Rooms_without_setup_get_none_and_setup_without_a_session_is_left_alone()
    {
        await migrator.MigrateAsync();

        Assert.False(await db.RoomGameSetups.AnyAsync(s => s.RoomId == twoTruths));
        var orphan = await db.RoomGameSetups.AsNoTracking().SingleAsync(s => s.RoomId == sessionless);
        Assert.Null(orphan.SessionId);
    }

    [Fact]
    public async Task Rolling_back_keeps_one_set_per_room_and_every_answer_then_applies_again()
    {
        await migrator.MigrateAsync();
        await migrator.MigrateAsync(Previous);

        var counts = await RoomScopedCountsAsync();
        Assert.Equal((3, 3, 1, 4), (counts.Choices, counts.Questions, counts.TriviaSetups, counts.Answers));
        var originals = triviaQuestions.ToArray();
        Assert.Equal(4, await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM \"TriviaAnswers\" WHERE \"QuestionId\" = ANY({originals})").SingleAsync());

        await migrator.MigrateAsync();

        Assert.Equal(9, await db.RoomChoices.CountAsync(c => c.RoomId == picker));
        Assert.Equal(6, await db.TriviaQuestions.CountAsync(q => q.RoomId == trivia));
        Assert.Equal(2, await db.RoomGameSetups.CountAsync(s => s.RoomId == trivia));
        Assert.Equal(4, await db.TriviaAnswers.CountAsync());
    }

    // Raw SQL, because after rolling back the current model has columns the database no longer has.
    private async Task<(int Choices, int Questions, int TriviaSetups, int Answers)> RoomScopedCountsAsync()
    {
        async Task<int> Count(FormattableString sql) => await db.Database.SqlQuery<int>(sql).SingleAsync();
        return (
            await Count($"SELECT count(*)::int AS \"Value\" FROM \"RoomChoices\" WHERE \"RoomId\" = {picker}"),
            await Count($"SELECT count(*)::int AS \"Value\" FROM \"TriviaQuestions\" WHERE \"RoomId\" = {trivia}"),
            await Count($"SELECT count(*)::int AS \"Value\" FROM \"RoomGameSetups\" WHERE \"RoomId\" = {trivia}"),
            await Count($"SELECT count(*)::int AS \"Value\" FROM \"TriviaAnswers\""));
    }
}
