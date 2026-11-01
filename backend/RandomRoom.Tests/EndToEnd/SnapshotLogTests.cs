using System.Text.Json;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>The bot's snapshot bookkeeping, tested without a server: every end-to-end assertion trusts it.</summary>
[Trait("Category", "EndToEnd")]
public sealed class SnapshotLogTests
{
    private readonly SnapshotLog log = new("test bot");

    [Fact]
    public void A_snapshot_older_than_the_one_held_is_remembered_but_does_not_replace_it()
    {
        var older = SnapshotWith(sequence: 40, status: "Active");

        log.Offer(SnapshotWith(sequence: 50, status: "Completed"));
        log.Offer(older);

        Assert.Equal("Completed", log.Latest!.Status);
        Assert.Contains(older, log.Received);
    }

    [Fact]
    public void A_newer_snapshot_replaces_the_one_held()
    {
        log.Offer(SnapshotWith(sequence: 40, status: "Active"));
        log.Offer(SnapshotWith(sequence: 50, status: "Completed"));

        Assert.Equal("Completed", log.Latest!.Status);
    }

    [Fact]
    public async Task Waiting_returns_when_a_matching_snapshot_arrives_later()
    {
        var waiting = log.WaitForAsync(s => s.IsCompleted, "completion");
        log.Offer(SnapshotWith(sequence: 1, status: "Active"));
        log.Offer(SnapshotWith(sequence: 2, status: "Completed"));

        Assert.True((await waiting).IsCompleted);
    }

    [Fact]
    public async Task Waiting_says_what_was_last_seen_when_it_times_out()
    {
        log.Offer(SnapshotWith(sequence: 1, status: "Active"));

        var timeout = await Assert.ThrowsAsync<TimeoutException>(
            () => log.WaitForAsync(s => s.IsCompleted, "completion", TimeSpan.FromMilliseconds(50)));

        Assert.Contains("Active", timeout.Message);
    }

    private static Snapshot SnapshotWith(long sequence, string status) =>
        new(JsonSerializer.SerializeToElement(new
        {
            sequence,
            session = new { status, number = 1 },
            gamePayload = new { },
            players = Array.Empty<object>(),
        }));
}
