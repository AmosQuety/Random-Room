using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public sealed class RoomRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();
    private readonly RoomDbContext db;
    private readonly ManualClock clock = new(Now);
    private readonly IGameEngine[] engines;
    private readonly RoomAdminService admin;
    private readonly GameSessionService sessions;

    public RoomRetentionTests()
    {
        db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        db.Database.Migrate();
        engines = [new RandomPickerEngine(db, new FakeRandomSource(0, 1, 0, 1), clock), new TriviaEngine(db, clock)];
        admin = new RoomAdminService(db, clock, engines);
        sessions = new GameSessionService(db, new PresenceTracker(), clock, engines);
    }

    public void Dispose()
    {
        db.Dispose();
        database.Dispose();
    }

    private RoomRetentionService Retention(int days = 30) =>
        new(db, clock, Options.Create(new RoomOptions { JwtSigningKey = new string('k', 32), RetentionDays = days }), NullLogger<RoomRetentionService>.Instance);

    /// <summary>Creates a room as if it were made <paramref name="daysAgo"/> days ago, then returns the clock to now.</summary>
    private async Task<Guid> RoomMadeAsync(int daysAgo, string gameType = RandomPickerEngine.Key)
    {
        clock.Advance(-TimeSpan.FromDays(daysAgo));
        object setup = gameType == TriviaEngine.Key
            ? new { questions = new[] { new { text = "2 + 2?", options = new[] { "3", "4" }, correctIndex = 1 } } }
            : new { choices = new[] { "Sarah", "Judith" } };
        var created = await admin.CreateRoomAsync(new CreateRoomRequest("Old room", gameType, TestSetup.Of(setup), ["Amos", "Lydia"], "Amos"));
        var roomId = (await db.Rooms.SingleAsync(r => r.Slug == created.Slug)).Id;
        clock.Advance(TimeSpan.FromDays(daysAgo));
        return roomId;
    }

    private async Task PlayAtAsync(Guid roomId, int daysAgo, Func<Task> play)
    {
        clock.Advance(-TimeSpan.FromDays(daysAgo));
        await sessions.StartSessionAsync(roomId, "Amos");
        await play();
        clock.Advance(TimeSpan.FromDays(daysAgo));
    }

    private Task<bool> ExistsAsync(Guid roomId) => db.Rooms.AnyAsync(r => r.Id == roomId);

    [Fact]
    public async Task A_room_with_no_activity_for_longer_than_the_retention_period_is_deleted_with_its_contents()
    {
        var old = await RoomMadeAsync(daysAgo: 31);

        var deleted = await Retention().DeleteInactiveRoomsAsync();

        Assert.Equal(1, deleted);
        Assert.False(await ExistsAsync(old));
        Assert.Empty(await db.RoomPlayers.Where(p => p.RoomId == old).ToListAsync());
        Assert.Empty(await db.GameSessions.Where(s => s.RoomId == old).ToListAsync());
        Assert.Empty(await db.RoomChoices.Where(c => c.RoomId == old).ToListAsync());
    }

    [Fact]
    public async Task A_recent_room_is_kept()
    {
        var recent = await RoomMadeAsync(daysAgo: 10);

        Assert.Equal(0, await Retention().DeleteInactiveRoomsAsync());
        Assert.True(await ExistsAsync(recent));
    }

    [Fact]
    public async Task A_room_exactly_inside_the_period_is_kept_and_one_just_outside_is_deleted()
    {
        var inside = await RoomMadeAsync(daysAgo: 29);
        var outside = await RoomMadeAsync(daysAgo: 31);

        await Retention().DeleteInactiveRoomsAsync();

        Assert.True(await ExistsAsync(inside));
        Assert.False(await ExistsAsync(outside));
    }

    [Fact]
    public async Task An_old_room_that_started_a_game_recently_is_kept()
    {
        var room = await RoomMadeAsync(daysAgo: 60);
        await PlayAtAsync(room, daysAgo: 2, play: () => Task.CompletedTask);

        Assert.Equal(0, await Retention().DeleteInactiveRoomsAsync());
        Assert.True(await ExistsAsync(room));
    }

    [Fact]
    public async Task A_room_is_deleted_even_when_it_holds_recorded_picks_which_are_normally_undeletable()
    {
        var room = await RoomMadeAsync(daysAgo: 60);
        await PlayAtAsync(room, daysAgo: 50, play: () => sessions.PerformActionAsync(room, "Lydia", "trigger", null));
        Assert.Equal(1, await db.RandomPickerEvents.CountAsync());

        await Retention().DeleteInactiveRoomsAsync();

        Assert.False(await ExistsAsync(room));
        Assert.Equal(0, await db.RandomPickerEvents.CountAsync());
    }

    [Fact]
    public async Task A_room_is_deleted_even_when_it_holds_recorded_trivia_answers()
    {
        var room = await RoomMadeAsync(daysAgo: 60, TriviaEngine.Key);
        await PlayAtAsync(room, daysAgo: 50, play: () => sessions.PerformActionAsync(room, "Lydia", "answer", JsonSerializer.SerializeToElement(new { optionIndex = 1 })));
        Assert.Equal(1, await db.TriviaAnswers.CountAsync());

        await Retention().DeleteInactiveRoomsAsync();

        Assert.False(await ExistsAsync(room));
        Assert.Equal(0, await db.TriviaAnswers.CountAsync());
        Assert.Equal(0, await db.TriviaQuestions.CountAsync());
    }

    [Fact]
    public async Task Deleting_an_old_room_leaves_every_other_room_untouched()
    {
        var keep = await RoomMadeAsync(daysAgo: 3, TriviaEngine.Key);
        await RoomMadeAsync(daysAgo: 90);
        var keptPlayers = await db.RoomPlayers.CountAsync(p => p.RoomId == keep);
        var keptQuestions = await db.TriviaQuestions.CountAsync(q => q.RoomId == keep);

        await Retention().DeleteInactiveRoomsAsync();

        Assert.True(await ExistsAsync(keep));
        Assert.Equal(keptPlayers, await db.RoomPlayers.CountAsync(p => p.RoomId == keep));
        Assert.Equal(keptQuestions, await db.TriviaQuestions.CountAsync(q => q.RoomId == keep));
    }

    [Fact]
    public async Task A_room_that_was_audited_is_deleted_with_its_audit_record_once_past_retention()
    {
        var room = await RoomMadeAsync(daysAgo: 45);
        db.RoomAuditEvents.Add(new RoomAuditEvent { Id = Guid.NewGuid(), RoomId = room, Actor = "Amos", Action = RoomAuditEvent.SeatReset, Target = "Lydia", OccurredAt = Now.AddDays(-45) });
        await db.SaveChangesAsync();

        await Retention().DeleteInactiveRoomsAsync();

        Assert.Equal(0, await db.RoomAuditEvents.CountAsync());
    }

    [Fact]
    public async Task A_retention_of_zero_turns_the_clean_up_off()
    {
        var old = await RoomMadeAsync(daysAgo: 400);

        Assert.Equal(0, await Retention(days: 0).DeleteInactiveRoomsAsync());
        Assert.True(await ExistsAsync(old));
    }

    [Fact]
    public async Task More_rooms_than_one_batch_are_all_deleted()
    {
        for (var i = 0; i < 105; i++) await RoomMadeAsync(daysAgo: 40 + i % 5);

        var deleted = await Retention().DeleteInactiveRoomsAsync();

        Assert.Equal(105, deleted);
        Assert.Equal(0, await db.Rooms.CountAsync());
    }

    [Fact]
    public async Task Running_it_twice_deletes_nothing_the_second_time()
    {
        await RoomMadeAsync(daysAgo: 40);
        await Retention().DeleteInactiveRoomsAsync();

        Assert.Equal(0, await Retention().DeleteInactiveRoomsAsync());
    }

    [Fact]
    public void A_negative_retention_is_not_a_valid_setting()
    {
        Assert.False(RoomOptions.IsValid(new RoomOptions { JwtSigningKey = new string('k', 32), RetentionDays = -1 }));
        Assert.True(RoomOptions.IsValid(new RoomOptions { JwtSigningKey = new string('k', 32), RetentionDays = 0 }));
    }
}
