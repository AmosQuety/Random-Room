using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Hubs;

/// <summary>Server-to-client push only, scoped per room. Clients perform actions through the REST endpoints.</summary>
[Authorize]
public sealed class RoomHub(PresenceTracker presence, GameSessionService room, IRoomNotifier notifier) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var roomId = Context.User!.GetRoomId();
        var player = Context.User!.GetPlayerName();

        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRRoomNotifier.GroupName(roomId));
        presence.Connected(Context.ConnectionId, roomId, player);
        await notifier.PublishAsync(await room.GetSnapshotAsync(roomId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roomId = Context.User!.GetRoomId();

        presence.Disconnected(Context.ConnectionId);
        await notifier.PublishAsync(await room.GetSnapshotAsync(roomId));
        await base.OnDisconnectedAsync(exception);
    }
}
