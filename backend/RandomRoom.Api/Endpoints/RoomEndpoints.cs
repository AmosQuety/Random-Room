using RandomRoom.Api.Auth;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Endpoints;

public sealed record JoinRequest(string? Player, string? Pin);

public sealed record JoinResponse(string Token, string Player, bool IsHost);

public static class RoomEndpoints
{
    public const string JoinRateLimitPolicy = "join";

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/join", Join).RequireRateLimiting(JoinRateLimitPolicy);

        var room = api.MapGroup("/room").RequireAuthorization();
        room.MapGet("/", (RoomService rooms, CancellationToken ct) => rooms.GetSnapshotAsync(ct));
        room.MapPost("/trigger", (HttpContext http, RoomService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (player) => rooms.TriggerRandomAsync(player, ct), ct));
        room.MapPost("/round/start", (HttpContext http, RoomService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (player) => rooms.StartRoundAsync(player, ct), ct));
        room.MapPost("/round/end", (HttpContext http, RoomService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (player) => rooms.EndRoundAsync(player, ct), ct));
        room.MapPost("/round/new", (HttpContext http, RoomService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (player) => rooms.StartNewRoundAsync(player, ct), ct));
    }

    private static IResult Join(JoinRequest request, PlayerTokenService tokens, Microsoft.Extensions.Options.IOptions<RoomOptions> options)
    {
        var token = tokens.TryIssueToken(request.Player ?? "", request.Pin ?? "");
        if (token is null)
        {
            // One generic answer for unknown player and wrong PIN.
            return Results.Problem("Name or PIN is incorrect.", statusCode: StatusCodes.Status401Unauthorized);
        }
        return Results.Ok(new JoinResponse(token, request.Player!, request.Player == options.Value.HostPlayer));
    }

    private static async Task<IResult> Act(
        HttpContext http, IRoomNotifier notifier, Func<string, Task<RoomSnapshot>> action, CancellationToken ct)
    {
        var snapshot = await action(http.User.GetPlayerName());
        await notifier.PublishAsync(snapshot, ct);
        return Results.Ok(snapshot);
    }
}
