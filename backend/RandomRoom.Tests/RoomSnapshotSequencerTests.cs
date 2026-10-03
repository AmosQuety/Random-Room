using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class RoomSnapshotSequencerTests
{
    [Fact]
    public async Task Sequence_only_goes_up_for_one_room()
    {
        var sequencer = new RoomSnapshotSequencer();
        var room = Guid.NewGuid();

        var first = await sequencer.RunAsync(room, seq => Task.FromResult(seq));
        var second = await sequencer.RunAsync(room, seq => Task.FromResult(seq));

        Assert.True(second > first);
    }

    [Fact]
    public async Task Builds_for_one_room_never_overlap_and_run_in_sequence_order()
    {
        var sequencer = new RoomSnapshotSequencer();
        var room = Guid.NewGuid();
        var running = 0;
        var maxRunning = 0;
        var seen = new List<long>();

        await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => sequencer.RunAsync(room, async seq =>
        {
            maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
            await Task.Delay(2);
            seen.Add(seq);
            Interlocked.Decrement(ref running);
            return seq;
        })));

        Assert.Equal(1, maxRunning);
        Assert.Equal(seen.OrderBy(s => s), seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task Different_rooms_do_not_wait_for_each_other()
    {
        var sequencer = new RoomSnapshotSequencer();
        var release = new TaskCompletionSource();
        var blocked = sequencer.RunAsync(Guid.NewGuid(), async seq =>
        {
            await release.Task;
            return seq;
        });

        var other = await sequencer.RunAsync(Guid.NewGuid(), seq => Task.FromResult(seq)).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(other > 0);
        release.SetResult();
        await blocked;
    }

    [Fact]
    public async Task A_new_sequencer_starts_ahead_of_an_old_one_so_clients_accept_snapshots_after_a_restart()
    {
        var room = Guid.NewGuid();
        var before = await new RoomSnapshotSequencer().RunAsync(room, seq => Task.FromResult(seq));
        await Task.Delay(5);

        var after = await new RoomSnapshotSequencer().RunAsync(room, seq => Task.FromResult(seq));

        Assert.True(after > before);
    }

    [Fact]
    public async Task A_failing_build_releases_the_room()
    {
        var sequencer = new RoomSnapshotSequencer();
        var room = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sequencer.RunAsync<long>(room, _ => throw new InvalidOperationException("boom")));

        var next = await sequencer.RunAsync(room, seq => Task.FromResult(seq)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(next > 0);
    }
}
