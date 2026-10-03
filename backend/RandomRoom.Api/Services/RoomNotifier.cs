using Microsoft.AspNetCore.SignalR;
using RandomRoom.Api.Hubs;

namespace RandomRoom.Api.Services;

public interface IRoomNotifier
{
    /// <summary>Sends one player their own view of the room. There is deliberately no room-wide send: views differ per player.</summary>
    Task PublishToPlayerAsync(Guid roomId, string player, RoomSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Tells everyone connected to a room that the host deleted it. Carries no room data.</summary>
    Task NotifyRoomDeletedAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Tells a player their seat was reset and stops sending anything to the connections they already have open.</summary>
    Task RevokePlayerAsync(Guid roomId, string player, IReadOnlyList<string> connectionIds, CancellationToken ct = default);
}

public sealed class SignalRRoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public const string SnapshotEvent = "roomChanged";
    public const string RevokedEvent = "seatReset";
    public const string RoomDeletedEvent = "roomDeleted";

    public static string GroupName(Guid roomId) => $"room:{roomId}";

    /// <summary>Every connection a player has open joins this group, so a push reaches all of their tabs and only theirs.</summary>
    public static string PlayerGroupName(Guid roomId, string player) => $"room:{roomId}:player:{player}";

    public Task PublishToPlayerAsync(Guid roomId, string player, RoomSnapshot snapshot, CancellationToken ct = default) =>
        hub.Clients.Group(PlayerGroupName(roomId, player)).SendAsync(SnapshotEvent, snapshot, ct);

    public Task NotifyRoomDeletedAsync(Guid roomId, CancellationToken ct = default) =>
        hub.Clients.Group(GroupName(roomId)).SendAsync(RoomDeletedEvent, ct);

    public async Task RevokePlayerAsync(Guid roomId, string player, IReadOnlyList<string> connectionIds, CancellationToken ct = default)
    {
        var group = PlayerGroupName(roomId, player);
        await hub.Clients.Group(group).SendAsync(RevokedEvent, ct);
        foreach (var connectionId in connectionIds)
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, ct);
            await hub.Groups.RemoveFromGroupAsync(connectionId, GroupName(roomId), ct);
        }
    }
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

    /// <summary>After a room is deleted: tells everyone still connected, then forgets their connections.</summary>
    public async Task AnnounceRoomDeletedAsync(Guid roomId, CancellationToken ct = default)
    {
        await notifier.NotifyRoomDeletedAsync(roomId, ct);
        presence.RemoveRoom(roomId);
    }

    /// <summary>Cuts a reset seat's open connections off from the room, so the old device receives nothing further.</summary>
    public Task RevokeSeatAsync(Guid roomId, string player, CancellationToken ct = default) =>
        notifier.RevokePlayerAsync(roomId, player, presence.RemovePlayer(roomId, player), ct);
}
