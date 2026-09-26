using Microsoft.AspNetCore.SignalR;
using RandomRoom.Api.Hubs;

namespace RandomRoom.Api.Services;

public interface IRoomNotifier
{
    Task PublishAsync(RoomSnapshot snapshot, CancellationToken ct = default);
}

public sealed class SignalRRoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public const string SnapshotEvent = "roomChanged";

    public Task PublishAsync(RoomSnapshot snapshot, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(SnapshotEvent, snapshot, ct);
}
