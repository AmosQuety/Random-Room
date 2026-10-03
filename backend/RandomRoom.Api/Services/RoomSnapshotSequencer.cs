using System.Collections.Concurrent;

namespace RandomRoom.Api.Services;

/// <summary>
/// Orders the snapshots built for a room. A client can receive the same room state by two routes (a SignalR push and
/// the response to its own action) and they may arrive in either order, so every snapshot carries a sequence number
/// and clients ignore anything older than what they already show.
///
/// Builds for one room run one at a time: the build that gets the higher sequence also reads the database later,
/// so a higher sequence always means state at least as fresh. Sequences start from the clock, so they still rise
/// after a server restart. Assumes a single server instance, as the rest of the app does.
/// </summary>
public sealed class RoomSnapshotSequencer
{
    public static RoomSnapshotSequencer Shared { get; } = new();

    private readonly ConcurrentDictionary<Guid, RoomSequence> rooms = new();

    /// <param name="ct">Stops waiting for a turn if the caller gives up, so an abandoned request does not queue up database work.</param>
    public async Task<T> RunAsync<T>(Guid roomId, Func<long, Task<T>> build, CancellationToken ct = default)
    {
        var room = rooms.GetOrAdd(roomId, _ => new RoomSequence());
        await room.Gate.WaitAsync(ct);
        try
        {
            return await build(room.Next());
        }
        finally
        {
            room.Gate.Release();
        }
    }

    private sealed class RoomSequence
    {
        private long last;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        // Called only while holding Gate.
        public long Next() => last = Math.Max(last + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
