using Microsoft.AspNetCore.SignalR;
using RandomRoom.Api.Hubs;

namespace RandomRoom.Api.Services;

public interface IRoomNotifier
{
    /// <summary>Sends one player their own view of the room. There is deliberately no room-wide send: views differ per player.</summary>
    Task PublishToPlayerAsync(Guid roomId, string player, RoomSnapshot snapshot, CancellationToken ct = default);
}

public sealed class SignalRRoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public const string SnapshotEvent = "roomChanged";

    public static string GroupName(Guid roomId) => $"room:{roomId}";

    /// <summary>Every connection a player has open joins this group, so a push reaches all of their tabs and only theirs.</summary>
    public static string PlayerGroupName(Guid roomId, string player) => $"room:{roomId}:player:{player}";

    public Task PublishToPlayerAsync(Guid roomId, string player, RoomSnapshot snapshot, CancellationToken ct = default) =>
        hub.Clients.Group(PlayerGroupName(roomId, player)).SendAsync(SnapshotEvent, snapshot, ct);
}

/// <summary>
/// Pushes a fresh, per-player snapshot to every online player after something changed. Built per viewer because
/// games with hidden information show different players different payloads.
/// </summary>
public sealed class RoomBroadcaster(GameSessionService rooms, PresenceTracker presence, IRoomNotifier notifier)
{
    public async Task PublishAsync(Guid roomId, CancellationToken ct = default)
    {
        foreach (var player in presence.OnlinePlayers(roomId))
            await notifier.PublishToPlayerAsync(roomId, player, await rooms.GetSnapshotForAsync(roomId, player, ct), ct);
    }
}
