using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Hubs;

/// <summary>Server-to-client push only. Clients perform actions through the REST endpoints.</summary>
[Authorize]
public sealed class RoomHub(PresenceTracker presence, RoomService room, IRoomNotifier notifier) : Hub
{
    public override async Task OnConnectedAsync()
    {
        presence.Connected(Context.ConnectionId, Context.User!.GetPlayerName());
        await notifier.PublishAsync(await room.GetSnapshotAsync());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Disconnected(Context.ConnectionId);
        await notifier.PublishAsync(await room.GetSnapshotAsync());
        await base.OnDisconnectedAsync(exception);
    }
}
