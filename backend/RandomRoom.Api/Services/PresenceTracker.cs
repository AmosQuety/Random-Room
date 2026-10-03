using System.Collections.Concurrent;

namespace RandomRoom.Api.Services;

/// <summary>Tracks which (room, player) pairs have at least one live connection. Single-instance, in memory.</summary>
public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, (Guid RoomId, string Player)> connections = new();

    public void Connected(string connectionId, Guid roomId, string player) =>
        connections[connectionId] = (roomId, player);

    public void Disconnected(string connectionId) => connections.TryRemove(connectionId, out _);

    public IReadOnlyList<string> OnlinePlayers(Guid roomId) =>
        connections.Values.Where(c => c.RoomId == roomId).Select(c => c.Player).Distinct().ToList();

    public bool IsOnline(Guid roomId, string player) =>
        connections.Values.Any(c => c.RoomId == roomId && c.Player == player);
}
