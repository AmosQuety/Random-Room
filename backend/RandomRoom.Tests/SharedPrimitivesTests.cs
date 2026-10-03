using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class PhaseGuardTests
{
    [Fact]
    public void Standard_flow_allows_the_legal_moves()
    {
        var guard = PhaseGuard.Standard;

        Assert.Equal(Phases.Collecting, guard.Move(Phases.Lobby, Phases.Collecting));
        Assert.Equal(Phases.Revealed, guard.Move(Phases.Collecting, Phases.Revealed));
        Assert.Equal(Phases.Collecting, guard.Move(Phases.Revealed, Phases.Collecting));
        Assert.Equal(Phases.Complete, guard.Move(Phases.Revealed, Phases.Complete));
    }

    [Theory]
    [InlineData(Phases.Lobby, Phases.Revealed)]
    [InlineData(Phases.Collecting, Phases.Collecting)]
    [InlineData(Phases.Collecting, Phases.Complete)]
    [InlineData(Phases.Complete, Phases.Collecting)]
    public void Illegal_moves_are_a_conflict(string from, string to)
    {
        var ex = Assert.Throws<RoomRuleException>(() => PhaseGuard.Standard.Move(from, to));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public void Require_rejects_the_wrong_phase()
    {
        var ex = Assert.Throws<RoomRuleException>(() => PhaseGuard.Require(Phases.Revealed, Phases.Collecting, "Too late."));

        Assert.Equal("Too late.", ex.Message);
    }
}

public class AnswerNormalizerTests
{
    [Theory]
    [InlineData("  The  Beatles! ", "the beatles")]
    [InlineData("CAFÉ", "cafe")]
    [InlineData("rock-n-roll", "rock n roll")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizes_case_accents_punctuation_and_spacing(string? input, string expected) =>
        Assert.Equal(expected, AnswerNormalizer.Normalize(input));

    [Fact]
    public void Matches_any_accepted_answer_ignoring_formatting()
    {
        Assert.True(AnswerNormalizer.MatchesAny("the LION king", ["Lion King", "The Lion King"]));
        Assert.False(AnswerNormalizer.MatchesAny("Aladdin", ["Lion King"]));
        Assert.False(AnswerNormalizer.MatchesAny("   ", ["Lion King"]));
    }
}

public class ServerTimerTests
{
    [Fact]
    public void A_deadline_is_computed_and_checked_on_the_server_clock()
    {
        var clock = new ManualClock();
        var deadline = ServerTimer.DeadlineIn(clock, 30);

        Assert.False(ServerTimer.HasPassed(clock, deadline));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(ServerTimer.HasPassed(clock, deadline));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(ServerTimer.HasPassed(clock, deadline));
    }

    [Fact]
    public void No_limit_means_no_deadline()
    {
        Assert.Null(ServerTimer.DeadlineIn(new ManualClock(), null));
        Assert.Null(ServerTimer.DeadlineIn(new ManualClock(), 0));
        Assert.False(ServerTimer.HasPassed(new ManualClock(), null));
    }
}

/// <summary>An engine with hidden information, to prove the per-viewer plumbing without any real game.</summary>
public sealed class SecretEngine(GameStore store) : IGameEngine
{
    public sealed record SecretPayload(string? Secret);

    public string GameType => "test-secret";

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        store.AddSetup(roomId, new { secret = "hunter2" });
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new { });
        return Task.CompletedTask;
    }

    public Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) => Task.FromResult(false);

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) => Task.FromResult<object>(new SecretPayload(null));

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        Task.FromResult<object>(new SecretPayload(viewer == "Amos" ? "hunter2" : null));

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) => Task.FromResult<object>(new { });
}

public class PerViewerSnapshotTests
{
    private static GameHarness Secret() => new(d => new SecretEngine(d.Store), "test-secret", new { });

    [Fact]
    public async Task Each_viewer_gets_only_what_they_may_see()
    {
        using var h = Secret();

        var amos = (SecretEngine.SecretPayload)(await h.SnapshotAsync("Amos")).GamePayload;
        var lydia = (SecretEngine.SecretPayload)(await h.SnapshotAsync("Lydia")).GamePayload;

        Assert.Equal("hunter2", amos.Secret);
        Assert.Null(lydia.Secret);
    }

    [Fact]
    public async Task The_viewerless_snapshot_is_the_safe_public_view()
    {
        using var h = Secret();

        var publicView = (SecretEngine.SecretPayload)(await h.PublicSnapshotAsync()).GamePayload;

        Assert.Null(publicView.Secret);
    }

    [Fact]
    public async Task Action_responses_are_built_for_the_actor()
    {
        using var h = Secret();
        await h.StartAsync();

        var asAmos = (SecretEngine.SecretPayload)(await h.ActAsync("Amos", "noop")).GamePayload;
        var asLydia = (SecretEngine.SecretPayload)(await h.ActAsync("Lydia", "noop")).GamePayload;

        Assert.Equal("hunter2", asAmos.Secret);
        Assert.Null(asLydia.Secret);
    }
}

public sealed class RecordingNotifier : IRoomNotifier
{
    public List<(string Player, RoomSnapshot Snapshot)> Sent { get; } = [];

    public Task PublishToPlayerAsync(Guid roomId, string player, RoomSnapshot snapshot, CancellationToken ct = default)
    {
        Sent.Add((player, snapshot));
        return Task.CompletedTask;
    }

    public List<Guid> DeletedRooms { get; } = [];

    public Task NotifyRoomDeletedAsync(Guid roomId, CancellationToken ct = default)
    {
        DeletedRooms.Add(roomId);
        return Task.CompletedTask;
    }

    public List<(string Player, IReadOnlyList<string> ConnectionIds)> Revoked { get; } = [];

    public Task RevokePlayerAsync(Guid roomId, string player, IReadOnlyList<string> connectionIds, CancellationToken ct = default)
    {
        Revoked.Add((player, connectionIds));
        return Task.CompletedTask;
    }
}

public class RoomBroadcasterTests
{
    [Fact]
    public async Task Publishes_a_separate_view_to_each_online_player_and_nobody_else()
    {
        using var h = new GameHarness(d => new SecretEngine(d.Store), "test-secret", new { });
        var presence = new PresenceTracker();
        presence.Connected("c1", h.RoomId, "Amos");
        presence.Connected("c2", h.RoomId, "Lydia");
        presence.Connected("c3", h.RoomId, "Lydia");
        var notifier = new RecordingNotifier();

        await new RoomBroadcaster(h.Service, presence, notifier).PublishAsync(h.RoomId);

        Assert.Equal(["Amos", "Lydia"], notifier.Sent.Select(s => s.Player).Order());
        var amos = (SecretEngine.SecretPayload)notifier.Sent.Single(s => s.Player == "Amos").Snapshot.GamePayload;
        var lydia = (SecretEngine.SecretPayload)notifier.Sent.Single(s => s.Player == "Lydia").Snapshot.GamePayload;
        Assert.Equal("hunter2", amos.Secret);
        Assert.Null(lydia.Secret);
    }
}

public class SeatRevocationTests
{
    [Fact]
    public async Task Revoking_a_seat_cuts_off_every_connection_that_player_has_and_nobody_elses()
    {
        using var h = new GameHarness(d => new SecretEngine(d.Store), "test-secret", new { });
        var presence = new PresenceTracker();
        presence.Connected("c1", h.RoomId, "Amos");
        presence.Connected("c2", h.RoomId, "Lydia");
        presence.Connected("c3", h.RoomId, "Lydia");
        var notifier = new RecordingNotifier();

        await new RoomBroadcaster(h.Service, presence, notifier).RevokeSeatAsync(h.RoomId, "Lydia");

        var revoked = Assert.Single(notifier.Revoked);
        Assert.Equal("Lydia", revoked.Player);
        Assert.Equal(["c2", "c3"], revoked.ConnectionIds.Order());
        Assert.True(presence.IsOnline(h.RoomId, "Amos"));
        Assert.False(presence.IsOnline(h.RoomId, "Lydia"));
    }
}

public class GameStoreTests
{
    private sealed record Data(int Count, string Note);

    private static GameHarness Harness() => new(d => new SecretEngine(d.Store), "test-secret", new { });

    private static Guid SessionOf(GameHarness h) => h.Db.GameSessions.Single(s => s.RoomId == h.RoomId).Id;

    [Fact]
    public async Task Setup_round_trips_through_the_database()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);

        var setup = await store.GetSetupAsync<Dictionary<string, string>>(h.RoomId, default);

        Assert.Equal("hunter2", setup["secret"]);
    }

    [Fact]
    public async Task State_survives_a_restart()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);
        var state = await store.LoadStateAsync<Dictionary<string, int>>(SessionOf(h), default);
        state.Phase = Phases.Collecting;
        state.Round = 3;
        state.Data = new Dictionary<string, int> { ["x"] = 7 };
        await store.SaveStateAsync(state, default);

        using var reopened = h.Reopen();
        var again = await new GameStore(reopened.Db, reopened.Clock).LoadStateAsync<Dictionary<string, int>>(SessionOf(reopened), default);

        Assert.Equal(Phases.Collecting, again.Phase);
        Assert.Equal(3, again.Round);
        Assert.Equal(7, again.Data["x"]);
    }

    [Fact]
    public async Task Two_writers_cannot_both_advance_the_same_state()
    {
        using var h = Harness();
        using var other = h.Reopen();
        var sessionId = SessionOf(h);
        var first = await new GameStore(h.Db, h.Clock).LoadStateAsync<Dictionary<string, int>>(sessionId, default);
        var second = await new GameStore(other.Db, other.Clock).LoadStateAsync<Dictionary<string, int>>(sessionId, default);

        first.Phase = Phases.Collecting;
        await new GameStore(h.Db, h.Clock).SaveStateAsync(first, default);
        second.Phase = Phases.Revealed;
        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => new GameStore(other.Db, other.Clock).SaveStateAsync(second, default));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public async Task One_entry_per_player_per_kind_per_round_is_enforced_by_the_database()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);
        var sessionId = SessionOf(h);
        await store.AddEntryAsync(sessionId, 0, "answer", "Amos", new Data(1, "a"), "Already answered.", default);

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() =>
            store.AddEntryAsync(sessionId, 0, "answer", "Amos", new Data(2, "b"), "Already answered.", default));
        Assert.Equal("Already answered.", ex.Message);

        // The rejected insert must not poison the context, and a different round or player is fine.
        await store.AddEntryAsync(sessionId, 1, "answer", "Amos", new Data(3, "c"), "Already answered.", default);
        await store.AddEntryAsync(sessionId, 0, "answer", "Lydia", new Data(4, "d"), "Already answered.", default);
        Assert.Equal(3, h.Db.GameEntries.Count());
    }

    [Fact]
    public async Task Entries_come_back_in_server_receipt_order()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);
        var sessionId = SessionOf(h);
        foreach (var player in new[] { "Jacob", "Amos", "Lydia" })
        {
            await store.AddEntryAsync(sessionId, 0, "buzz", player, new Data(0, ""), "dup", default);
            h.Clock.Advance(TimeSpan.FromMilliseconds(5));
        }

        var entries = await store.EntriesAsync<Data>(sessionId, "buzz", 0, default);

        Assert.Equal(["Jacob", "Amos", "Lydia"], entries.Select(e => e.Player));
        Assert.True(entries[0].ServerTime < entries[2].ServerTime);
    }

    [Fact]
    public async Task Exactly_one_of_many_simultaneous_claimants_wins()
    {
        using var h = Harness();
        var sessionId = SessionOf(h);
        var contenders = Enumerable.Range(0, 8).Select(i => h.Reopen()).ToList();
        try
        {
            var results = await Task.WhenAll(contenders.Select((c, i) =>
                new GameStore(c.Db, c.Clock).TryClaimAsync(sessionId, 0, $"P{i}", default)));

            Assert.Equal(1, results.Count(won => won));
        }
        finally
        {
            contenders.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public async Task A_claim_for_a_finished_round_does_not_count()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);

        Assert.False(await store.TryClaimAsync(SessionOf(h), 5, "Amos", default));
    }

    [Fact]
    public async Task A_multi_step_write_rolls_back_as_a_unit()
    {
        using var h = Harness();
        var store = new GameStore(h.Db, h.Clock);
        var sessionId = SessionOf(h);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InTransactionAsync(async () =>
        {
            await store.AddEntryAsync(sessionId, 0, "answer", "Amos", new Data(1, "a"), "dup", default);
            throw new InvalidOperationException("boom");
        }, default));

        h.Db.ChangeTracker.Clear();
        Assert.Equal(0, await h.Db.GameEntries.CountAsync());
    }
}

public class ContentBankTests
{
    private sealed record Item(string Text);

    [Fact]
    public void A_missing_bank_is_an_error_not_an_empty_list() =>
        Assert.Throws<InvalidOperationException>(() => ContentBank.Load<Item>("does-not-exist"));
}
