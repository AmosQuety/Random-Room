using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class RoomServiceTests
{
    private static async Task<RoomSnapshot> StartedRoom(RoomTestHarness h)
    {
        return await h.Room.StartRoundAsync("Amos");
    }

    [Fact]
    public async Task New_room_is_waiting_and_nobody_has_triggered()
    {
        using var h = new RoomTestHarness();

        var snapshot = await h.Room.GetSnapshotAsync();

        Assert.Equal(RoundStatus.Waiting, snapshot.Round.Status);
        Assert.All(snapshot.Players, p => Assert.False(p.HasTriggered));
        Assert.Empty(snapshot.Activity);
    }

    [Fact]
    public async Task Trigger_uses_the_server_random_source_and_publishes_result()
    {
        using var h = new RoomTestHarness(1);
        await StartedRoom(h);

        var snapshot = await h.Room.TriggerRandomAsync("Lydia");

        Assert.Equal("Judith", snapshot.Players.Single(p => p.Name == "Lydia").Result);
        var entry = Assert.Single(snapshot.Activity);
        Assert.Equal(("Lydia", "Judith"), (entry.TriggeredBy, entry.Result));
    }

    [Fact]
    public async Task Trigger_before_round_is_started_is_rejected()
    {
        using var h = new RoomTestHarness();

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.TriggerRandomAsync("Amos"));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
    }

    [Fact]
    public async Task Player_cannot_trigger_twice_in_one_round()
    {
        using var h = new RoomTestHarness(0, 1);
        await StartedRoom(h);
        await h.Room.TriggerRandomAsync("James");

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.TriggerRandomAsync("James"));

        Assert.Equal(RuleViolation.Conflict, ex.Violation);
        Assert.Single((await h.Room.GetSnapshotAsync()).Activity);
    }

    [Theory]
    [InlineData("Mallory")]
    [InlineData("amos")]
    [InlineData("")]
    public async Task Unknown_players_are_forbidden(string name)
    {
        using var h = new RoomTestHarness();
        await StartedRoom(h);

        var ex = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.TriggerRandomAsync(name));

        Assert.Equal(RuleViolation.Forbidden, ex.Violation);
    }

    [Fact]
    public async Task Round_completes_automatically_when_all_four_have_triggered_and_tally_adds_up()
    {
        using var h = new RoomTestHarness(0, 1, 1, 1);
        await StartedRoom(h);

        foreach (var player in RoomTestHarness.Players) await h.Room.TriggerRandomAsync(player);
        var snapshot = await h.Room.GetSnapshotAsync();

        Assert.Equal(RoundStatus.Completed, snapshot.Round.Status);
        Assert.Equal(1, snapshot.Tally.Single(t => t.Choice == "Sarah").Count);
        Assert.Equal(3, snapshot.Tally.Single(t => t.Choice == "Judith").Count);
    }

    [Theory]
    [InlineData("Lydia")]
    [InlineData("James")]
    public async Task Only_the_host_controls_rounds(string notHost)
    {
        using var h = new RoomTestHarness();

        var start = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.StartRoundAsync(notHost));
        await StartedRoom(h);
        var end = await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.EndRoundAsync(notHost));

        Assert.Equal(RuleViolation.Forbidden, start.Violation);
        Assert.Equal(RuleViolation.Forbidden, end.Violation);
    }

    [Fact]
    public async Task Ended_round_locks_results_and_rejects_further_triggers()
    {
        using var h = new RoomTestHarness(1);
        await StartedRoom(h);
        await h.Room.TriggerRandomAsync("Jacob");
        await h.Room.EndRoundAsync("Amos");

        await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.TriggerRandomAsync("Lydia"));
        var snapshot = await h.Room.GetSnapshotAsync();

        Assert.Equal(RoundStatus.Completed, snapshot.Round.Status);
        Assert.Equal("Judith", snapshot.Players.Single(p => p.Name == "Jacob").Result);
        Assert.Null(snapshot.Players.Single(p => p.Name == "Lydia").Result);
    }

    [Fact]
    public async Task New_round_requires_completed_round_and_resets_players_but_keeps_history()
    {
        using var h = new RoomTestHarness(0, 1);
        await StartedRoom(h);
        await h.Room.TriggerRandomAsync("Amos");
        await Assert.ThrowsAsync<RoomRuleException>(() => h.Room.StartNewRoundAsync("Amos"));
        await h.Room.EndRoundAsync("Amos");

        var snapshot = await h.Room.StartNewRoundAsync("Amos");
        snapshot = await h.Room.TriggerRandomAsync("Amos");

        Assert.Equal(2, snapshot.Round.Number);
        Assert.Equal(RoundStatus.Active, snapshot.Round.Status);
        Assert.Equal(2, snapshot.Activity.Count);
        Assert.Equal(new[] { 2, 1 }, snapshot.Activity.Select(a => a.RoundNumber));
    }

    [Fact]
    public async Task Random_events_cannot_be_modified_once_recorded()
    {
        using var h = new RoomTestHarness(0);
        await StartedRoom(h);
        await h.Room.TriggerRandomAsync("Amos");

        var recorded = h.Db.RandomEvents.Single();
        recorded.Result = "Judith";

        Assert.Throws<InvalidOperationException>(() => h.Db.SaveChanges());
    }

    [Fact]
    public async Task Snapshot_reflects_presence()
    {
        using var h = new RoomTestHarness();
        h.Presence.Connected("conn-1", h.RoomId, "Lydia");

        var snapshot = await h.Room.GetSnapshotAsync();

        Assert.True(snapshot.Players.Single(p => p.Name == "Lydia").Online);
        Assert.False(snapshot.Players.Single(p => p.Name == "Jacob").Online);
    }

    [Fact]
    public async Task Player_stays_online_until_their_last_connection_closes()
    {
        using var h = new RoomTestHarness();
        h.Presence.Connected("phone", h.RoomId, "Lydia");
        h.Presence.Connected("laptop", h.RoomId, "Lydia");

        h.Presence.Disconnected("phone");

        Assert.True((await h.Room.GetSnapshotAsync()).Players.Single(p => p.Name == "Lydia").Online);
    }

    [Fact]
    public void Crypto_source_stays_in_range_and_reaches_both_choices()
    {
        var source = new CryptoRandomChoiceSource();

        var draws = Enumerable.Range(0, 400).Select(_ => source.PickIndex(2)).ToList();

        Assert.All(draws, d => Assert.InRange(d, 0, 1));
        Assert.Contains(0, draws);
        Assert.Contains(1, draws);
    }
}
