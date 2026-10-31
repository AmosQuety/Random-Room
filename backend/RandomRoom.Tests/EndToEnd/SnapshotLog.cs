namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// What one client has been sent. A snapshot can arrive by a push and by the response to the client's own action,
/// in either order, so the latest is chosen by sequence, never by arrival. Everything ever received is kept so a
/// test can scan it for secrets.
/// </summary>
public sealed class SnapshotLog(string owner)
{
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);

    private readonly object gate = new();
    private readonly List<Snapshot> received = [];
    private TaskCompletionSource changed = NewSignal();
    private Snapshot? latest;

    public Snapshot? Latest
    {
        get { lock (gate) return latest; }
    }

    public IReadOnlyList<Snapshot> Received
    {
        get { lock (gate) return [.. received]; }
    }

    /// <summary>An older snapshot is remembered as received but never replaces a newer one.</summary>
    public void Offer(Snapshot snapshot)
    {
        TaskCompletionSource previous;
        lock (gate)
        {
            received.Add(snapshot);
            if (latest is not null && snapshot.Sequence < latest.Sequence) return;
            latest = snapshot;
            previous = changed;
            changed = NewSignal();
        }
        previous.TrySetResult();
    }

    /// <summary>Waits until the latest snapshot satisfies the condition. The failure says what was last seen.</summary>
    public async Task<Snapshot> WaitForAsync(Func<Snapshot, bool> condition, string description, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultWait;
        var deadline = Task.Delay(limit);
        while (true)
        {
            Task signal;
            lock (gate)
            {
                if (latest is not null && condition(latest)) return latest;
                signal = changed.Task;
            }

            if (await Task.WhenAny(signal, deadline) == deadline)
                throw new TimeoutException($"{owner} waited {limit.TotalSeconds:0}s for {description}. Last seen: {Describe(Latest)}");
        }
    }

    private static string Describe(Snapshot? snapshot) =>
        snapshot is null ? "nothing" : $"sequence {snapshot.Sequence}, {snapshot.Status}, {snapshot.Payload.GetRawText()}";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
