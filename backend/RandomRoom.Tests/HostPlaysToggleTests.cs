using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Buzzer;
using RandomRoom.Api.Games.ForbiddenWords;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.SpinWheel;
using RandomRoom.Api.Services;
using Xunit.Abstractions;

namespace RandomRoom.Tests;

/// <summary>The host's "play in the next game" choice: who may change it, when, and that a game never sees it change.</summary>
public class HostPlaysToggleTests(ITestOutputHelper output)
{
    private const string Host = "Rita";
    private static readonly string[] Seats = [Host, "Lydia", "James", "Jacob"];

    /// <summary>A game that remembers who the participants were when each session was built.</summary>
    private sealed class RecordingEngine(GameStore store, ConcurrentDictionary<Guid, IReadOnlyList<string>> built, int maxPlayers) : IGameEngine
    {
        public const string Key = "recording";
        public string GameType => Key;
        public int MaxPlayers => maxPlayers;

        public Task ConfigureSessionAsync(Guid roomId, Guid sessionId, JsonElement setup, CancellationToken ct) => Task.CompletedTask;

        public async Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
            built[sessionId] = await store.PlayerNamesAsync(roomId, ct);

        public Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) => Task.FromResult(false);
        public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) => Task.FromResult<object>(new { });
        public Task<object> GetRoomPreviewAsync(Guid roomId, Guid sessionId, CancellationToken ct) => Task.FromResult<object>(new { });
    }

    private static GameHarness RecordingRoom(ConcurrentDictionary<Guid, IReadOnlyList<string>> built, bool hostPlays, string[]? seats = null, int maxPlayers = 12) =>
        new(d => new RecordingEngine(d.Store, built, maxPlayers), RecordingEngine.Key, new { }, seats ?? Seats, hostPlays: hostPlays);

    private static GameHarness PickerRoom(bool hostPlays, string[]? seats = null) =>
        new(d => new RandomPickerEngine(d.Db, d.Random, d.Clock), RandomPickerEngine.Key, new { choices = new[] { "A", "B" } }, seats ?? Seats, hostPlays: hostPlays);

    private static async Task BetweenGamesAsync(GameHarness room)
    {
        await room.StartAsync();
        await room.EndAsync();
    }

    private static Task<RoomSnapshot> ToggleAsync(GameHarness room, bool plays, string actor = Host) =>
        room.Service.SetHostPlaysAsync(room.RoomId, actor, plays);

    [Fact]
    public async Task Host_can_switch_to_playing_between_games_and_it_applies_from_the_next_game()
    {
        var built = new ConcurrentDictionary<Guid, IReadOnlyList<string>>();
        using var room = RecordingRoom(built, hostPlays: false);
        await BetweenGamesAsync(room);

        var toggled = await ToggleAsync(room, plays: true);
        var next = await room.NewRoundAsync();

        Assert.True(toggled.HostPlays);
        Assert.False(toggled.Players.Single(p => p.Name == Host).Plays);
        Assert.True(next.Players.Single(p => p.Name == Host).Plays);
        Assert.Contains(Host, built[next.Session.Id]);
    }

    [Fact]
    public async Task Host_can_switch_back_to_refereeing()
    {
        var built = new ConcurrentDictionary<Guid, IReadOnlyList<string>>();
        using var room = RecordingRoom(built, hostPlays: true);
        await BetweenGamesAsync(room);

        await ToggleAsync(room, plays: false);
        var next = await room.NewRoundAsync();

        Assert.False(next.HostPlays);
        Assert.DoesNotContain(Host, built[next.Session.Id]);
    }

    [Fact]
    public async Task Only_the_host_can_change_it()
    {
        using var room = PickerRoom(hostPlays: false);
        await BetweenGamesAsync(room);

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: true, actor: "Lydia"));

        Assert.Equal(RuleViolation.Forbidden, error.Violation);
        Assert.False((await room.PublicSnapshotAsync()).HostPlays);
        Assert.Empty(await room.Db.RoomAuditEvents.AsNoTracking().Where(e => e.Action == RoomAuditEvent.HostPlaysChanged).ToListAsync());
    }

    [Fact]
    public async Task Refused_before_the_first_game_starts()
    {
        using var room = PickerRoom(hostPlays: false);

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: true));

        Assert.Equal(RuleViolation.Conflict, error.Violation);
    }

    [Fact]
    public async Task Refused_during_a_game()
    {
        using var room = PickerRoom(hostPlays: false);
        await room.StartAsync();

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: true));

        Assert.Equal(RuleViolation.Conflict, error.Violation);
        Assert.False((await room.PublicSnapshotAsync()).HostPlays);
    }

    [Fact]
    public async Task A_change_is_audited_with_who_what_and_from_which_game()
    {
        using var room = PickerRoom(hostPlays: false);
        await BetweenGamesAsync(room);

        await ToggleAsync(room, plays: true);

        var audit = await room.Db.RoomAuditEvents.AsNoTracking().SingleAsync(e => e.RoomId == room.RoomId && e.Action == RoomAuditEvent.HostPlaysChanged);
        Assert.Equal(Host, audit.Actor);
        Assert.Equal("referee->plays from game 2", audit.Target);
        Assert.Equal(room.Clock.GetUtcNow(), audit.OccurredAt);
    }

    [Fact]
    public async Task Sending_the_current_value_changes_nothing_and_is_not_audited()
    {
        using var room = PickerRoom(hostPlays: false);
        await BetweenGamesAsync(room);

        var snapshot = await ToggleAsync(room, plays: false);

        Assert.False(snapshot.HostPlays);
        Assert.Empty(await room.Db.RoomAuditEvents.AsNoTracking().Where(e => e.Action == RoomAuditEvent.HostPlaysChanged).ToListAsync());
    }

    [Fact]
    public async Task Buzzer_refuses_a_host_who_wants_to_play()
    {
        using var room = new GameHarness(d => new BuzzerEngine(d.Store, d.Random), BuzzerEngine.Key,
            new { prompts = new[] { "Name a fruit" }, useBuiltIn = false, rounds = 1 }, Seats, hostPlays: false);
        await BetweenGamesAsync(room);

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: true));

        Assert.Equal(RuleViolation.InvalidInput, error.Violation);
        Assert.Contains("always judges", error.Message);
        Assert.False((await room.PublicSnapshotAsync()).HostPlays);
    }

    [Fact]
    public async Task Turning_it_on_cannot_push_the_game_over_its_most_players()
    {
        var built = new ConcurrentDictionary<Guid, IReadOnlyList<string>>();
        using var room = RecordingRoom(built, hostPlays: false, maxPlayers: 3);
        await BetweenGamesAsync(room);

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: true));

        Assert.Equal(RuleViolation.InvalidInput, error.Violation);
        Assert.Equal("This game works with at most 3 players.", error.Message);
    }

    [Fact]
    public async Task Turning_it_off_cannot_leave_too_few_players()
    {
        using var room = PickerRoom(hostPlays: true, seats: [Host, "Lydia"]);
        await BetweenGamesAsync(room);

        var error = await GameHarness.RejectedAsync(() => ToggleAsync(room, plays: false));

        Assert.Equal(RuleViolation.InvalidInput, error.Violation);
        Assert.Equal("This game needs at least 2 players, not counting the host.", error.Message);
        Assert.True((await room.PublicSnapshotAsync()).HostPlays);
    }

    // "Start new game" takes the room row lock. A share lock from another connection must hold it: it blocks FOR UPDATE
    // but not the key-share lock a plain insert of a session takes, so it tests the explicit lock.
    [Fact]
    public async Task Start_new_game_waits_for_the_room_lock()
    {
        using var room = PickerRoom(hostPlays: false);
        await BetweenGamesAsync(room);

        await AssertWaitsForRoomLockAsync(room, () => room.NewRoundAsync());
    }

    private static async Task AssertWaitsForRoomLockAsync(GameHarness room, Func<Task> action)
    {
        using var locker = room.Reopen();
        var transaction = await locker.Db.Database.BeginTransactionAsync();
        await locker.Db.Database.ExecuteSqlAsync($"SELECT 1 FROM \"Rooms\" WHERE \"Id\" = {room.RoomId} FOR SHARE");

        var pending = action();
        var first = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromMilliseconds(500)));
        Assert.NotSame(pending, first);

        await transaction.RollbackAsync();
        await pending;
    }

    // Whichever of the two wins, the new game is built from the participant list the room ends up with, and a toggle
    // that loses is refused rather than changing who plays under a game that has already been built.
    [Fact]
    public async Task Toggle_racing_start_new_game_never_changes_a_game_that_was_built()
    {
        var built = new ConcurrentDictionary<Guid, IReadOnlyList<string>>();
        using var room = RecordingRoom(built, hostPlays: false);
        using var other = room.Reopen();
        await BetweenGamesAsync(room);
        var (toggleWon, newGameWon) = (0, 0);

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var before = (await room.PublicSnapshotAsync()).HostPlays;
            // Both wait on one gate and are launched in alternating order, so each gets to win some of the time.
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> toggle;
            Task<RoomSnapshot> next;
            if (attempt % 2 == 0)
            {
                toggle = Task.Run(async () => { await gate.Task; return await TryToggleAsync(room, !before); });
                next = Task.Run(async () => { await gate.Task; return await other.NewRoundAsync(); });
            }
            else
            {
                next = Task.Run(async () => { await gate.Task; return await other.NewRoundAsync(); });
                toggle = Task.Run(async () => { await gate.Task; return await TryToggleAsync(room, !before); });
            }
            gate.SetResult();
            var (toggled, started) = (await toggle, await next);

            var after = (await room.PublicSnapshotAsync()).HostPlays;
            Assert.Equal(after, built[started.Session.Id].Contains(Host));
            if (toggled) { Assert.Equal(!before, after); toggleWon++; }
            else { Assert.Equal(before, after); newGameWon++; }

            await room.EndAsync();
        }

        output.WriteLine($"Toggle won {toggleWon}, new game won {newGameWon}.");
        Assert.Equal(30, toggleWon + newGameWon);
    }

    private static async Task<bool> TryToggleAsync(GameHarness room, bool plays)
    {
        try
        {
            await ToggleAsync(room, plays);
            return true;
        }
        catch (RoomRuleException e) when (e.Violation == RuleViolation.Conflict)
        {
            return false;
        }
    }

    [Fact]
    public async Task Spin_wheel_host_who_switches_to_playing_still_cannot_award_themselves()
    {
        using var room = new GameHarness(d => new SpinWheelEngine(d.Store, d.Random), SpinWheelEngine.Key,
            new { segments = new[] { "Sing", "Dance" }, useBuiltIn = false, spins = 3 }, [Host, "Lydia", "James"], new SeededRandom(), hostPlays: false);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: true);
        await room.NewRoundAsync();

        for (var round = 1; round <= 3; round++)
        {
            var spinner = (await room.PayloadAsync<WheelPayload>(Host)).Spinner!;
            await room.ActAsync(spinner, "spin");
            if (spinner == Host)
            {
                var error = await GameHarness.RejectedAsync(() => room.ActAsync(Host, "award"));
                Assert.Equal("You cannot give yourself the point.", error.Message);
                Assert.Equal("players", (await room.PayloadAsync<WheelPayload>(Host)).AwardMode);
                await room.ActAsync("Lydia", "award");
            }
            else
            {
                await room.ActAsync(Host, "award");
            }
            if (round < 3) await room.ActAsync(Host, "next");
        }

        var scoreboard = (await room.PayloadAsync<WheelPayload>(Host)).Scoreboard;
        Assert.Contains(scoreboard, s => s.Player == Host && s.Score == 1);
    }

    [Fact]
    public async Task Forbidden_words_host_who_switches_to_playing_flags_only_as_judge()
    {
        using var room = new GameHarness(d => new ForbiddenWordsEngine(d.Store, d.Random, d.Clock), ForbiddenWordsEngine.Key,
            new
            {
                cards = new[] { new { word = "Apple", forbidden = new[] { "Fruit", "Red", "Tree" } }, new { word = "River", forbidden = new[] { "Water", "Flow", "Bank" } }, new { word = "Chair", forbidden = new[] { "Sit", "Seat", "Legs" } }, new { word = "Bread", forbidden = new[] { "Bake", "Loaf", "Toast" } } },
                useBuiltIn = false,
                rounds = 4,
                timeLimitSeconds = 60
            },
            Seats, new SeededRandom(), hostPlays: false);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: true);
        await room.NewRoundAsync();
        var (refused, flaggedAsJudge) = (false, false);

        for (var round = 1; round <= 4; round++)
        {
            var view = await room.PayloadAsync<ForbiddenPayload>(Host);
            Assert.NotEqual("referee", view.Role);
            if (view.Judge == Host)
            {
                var flagged = (ForbiddenPayload)(await room.ActAsync(Host, "flag")).GamePayload;
                Assert.Contains(flagged.HostScoring, n => n.Reason == "The host, as judge, flagged a slip");
                flaggedAsJudge = true;
            }
            else
            {
                await GameHarness.RejectedAsync(() => room.ActAsync(Host, "flag"));
                Assert.Null(view.Describer == Host ? null : view.Card);
                refused = true;
                await room.ActAsync(Host, "reveal");
            }
            if (round < 4) await room.ActAsync(Host, "next");
        }

        Assert.True(refused);
        Assert.True(flaggedAsJudge);
    }

    // A finished game keeps the participants it was played with: the toggle is about the next game only.
    private static GameHarness WheelRoom(bool hostPlays) =>
        new(d => new SpinWheelEngine(d.Store, d.Random), SpinWheelEngine.Key,
            new { segments = new[] { "Sing", "Dance" }, useBuiltIn = false, spins = 3 }, Seats, new SeededRandom(), hostPlays: hostPlays);

    private static GameHarness ForbiddenRoom(bool hostPlays) =>
        new(d => new ForbiddenWordsEngine(d.Store, d.Random, d.Clock), ForbiddenWordsEngine.Key,
            new { cards = new[] { new { word = "Apple", forbidden = new[] { "Fruit", "Red", "Tree" } } }, useBuiltIn = false, rounds = 1, timeLimitSeconds = 60 },
            Seats, new SeededRandom(), hostPlays: hostPlays);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Toggle_does_not_change_the_finished_spin_wheel_scoreboard(bool hostPlayed)
    {
        using var room = WheelRoom(hostPlayed);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: !hostPlayed);

        var scoreboard = (await room.PayloadAsync<WheelPayload>("Lydia")).Scoreboard;

        Assert.Equal(hostPlayed, scoreboard.Any(s => s.Player == Host));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Toggle_does_not_change_the_finished_forbidden_words_scoreboard(bool hostPlayed)
    {
        using var room = ForbiddenRoom(hostPlayed);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: !hostPlayed);

        var scoreboard = (await room.PayloadAsync<ForbiddenPayload>("Lydia")).Scoreboard;

        Assert.Equal(hostPlayed, scoreboard.Any(s => s.Player == Host));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Toggle_does_not_change_who_played_the_finished_game_but_says_what_comes_next(bool hostPlayed)
    {
        using var room = PickerRoom(hostPlayed);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: !hostPlayed);

        var snapshot = await room.PublicSnapshotAsync();

        Assert.Equal(hostPlayed, snapshot.Players.Single(p => p.Name == Host).Plays);
        Assert.Equal(!hostPlayed, snapshot.HostPlays);
    }

    [Fact]
    public async Task After_the_next_game_starts_the_host_plays_as_chosen()
    {
        using var room = WheelRoom(hostPlays: false);
        await BetweenGamesAsync(room);
        await ToggleAsync(room, plays: true);

        var next = await room.NewRoundAsync();

        Assert.True(next.Players.Single(p => p.Name == Host).Plays);
        Assert.Contains(((WheelPayload)next.GamePayload).Scoreboard, s => s.Player == Host);
    }

    // Rooms made through the API keep the old default, so a Buzzer room can have HostPlays true: asking for the
    // value it already has is a no-op, not a refusal.
    [Fact]
    public async Task Buzzer_room_that_already_has_the_host_playing_accepts_the_same_value()
    {
        using var room = new GameHarness(d => new BuzzerEngine(d.Store, d.Random), BuzzerEngine.Key,
            new { prompts = new[] { "Name a fruit" }, useBuiltIn = false, rounds = 1 }, Seats, hostPlays: true);
        await BetweenGamesAsync(room);

        var snapshot = await ToggleAsync(room, plays: true);

        Assert.True(snapshot.HostPlays);
        Assert.Empty(await room.Db.RoomAuditEvents.AsNoTracking().Where(e => e.Action == RoomAuditEvent.HostPlaysChanged).ToListAsync());
    }
}
