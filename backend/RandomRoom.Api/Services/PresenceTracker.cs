using System.Collections.Concurrent;

namespace RandomRoom.Api.Services;

/// <summary>Tracks which players have at least one live connection. Single-instance, in memory.</summary>
public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, string> playerByConnection = new();

    public void Connected(string connectionId, string player) => playerByConnection[connectionId] = player;

    public void Disconnected(string connectionId) => playerByConnection.TryRemove(connectionId, out _);

    public bool IsOnline(string player) => playerByConnection.Values.Contains(player);
}
