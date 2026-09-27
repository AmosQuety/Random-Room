using System.Text.Json;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Endpoints;

public sealed record JoinRequest(string? RoomSlug, string? Player, string? Pin);

public sealed record JoinResponse(string Token, string Player, string RoomSlug, bool IsHost);

public sealed record CreateRoomHttpRequest(string Title, List<string> Choices, List<string> Players, string HostPlayer);

public sealed record ClaimInviteRequest(string Pin);

public sealed record ClaimInviteResponse(string Player);

public sealed record ActionRequest(string Action, JsonElement? Payload);

public static class RoomEndpoints
{
    public const string JoinRateLimitPolicy = "join";
    public const string RoomAdminRateLimitPolicy = "room-admin";

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/join", Join).RequireRateLimiting(JoinRateLimitPolicy);

        var rooms = api.MapGroup("/rooms");
        rooms.MapPost("/", CreateRoom).RequireRateLimiting(RoomAdminRateLimitPolicy);
        rooms.MapGet("/{slug}", GetPreview);
        rooms.MapPost("/{slug}/claim/{token}", ClaimInvite).RequireRateLimiting(RoomAdminRateLimitPolicy);

        var room = api.MapGroup("/room").RequireAuthorization();
        room.MapGet("/", (GameSessionService rooms, HttpContext http, CancellationToken ct) =>
            rooms.GetSnapshotAsync(http.User.GetRoomId(), ct));
        room.MapPost("/action", (HttpContext http, ActionRequest request, GameSessionService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (roomId, player) => rooms.PerformActionAsync(roomId, player, request.Action, request.Payload, ct), ct));
        room.MapPost("/session/start", (HttpContext http, GameSessionService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (roomId, player) => rooms.StartSessionAsync(roomId, player, ct), ct));
        room.MapPost("/session/end", (HttpContext http, GameSessionService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (roomId, player) => rooms.EndSessionAsync(roomId, player, ct), ct));
        room.MapPost("/session/new", (HttpContext http, GameSessionService rooms, IRoomNotifier notifier, CancellationToken ct) =>
            Act(http, notifier, (roomId, player) => rooms.StartNewSessionAsync(roomId, player, ct), ct));
    }

    private static async Task<IResult> Join(JoinRequest request, PlayerTokenService tokens, CancellationToken ct)
    {
        var issued = await tokens.TryIssueTokenAsync(request.RoomSlug ?? "", request.Player ?? "", request.Pin ?? "", ct);
        if (issued is null)
        {
            // One generic answer for unknown room, unknown player, and wrong PIN.
            return Results.Problem("Room, name, or PIN is incorrect.", statusCode: StatusCodes.Status401Unauthorized);
        }
        return Results.Ok(new JoinResponse(issued.Token, request.Player!, issued.RoomSlug, issued.IsHost));
    }

    private static async Task<IResult> CreateRoom(CreateRoomHttpRequest request, RoomAdminService admin, CancellationToken ct)
    {
        var result = await admin.CreateRoomAsync(
            new CreateRoomRequest(request.Title, request.Choices, request.Players, request.HostPlayer), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPreview(string slug, RoomAdminService admin, CancellationToken ct) =>
        Results.Ok(await admin.GetPreviewAsync(slug, ct));

    private static async Task<IResult> ClaimInvite(
        string slug, string token, ClaimInviteRequest request, RoomAdminService admin, CancellationToken ct)
    {
        var player = await admin.ClaimInviteAsync(slug, token, request.Pin, ct);
        return Results.Ok(new ClaimInviteResponse(player));
    }

    private static async Task<IResult> Act(
        HttpContext http, IRoomNotifier notifier, Func<Guid, string, Task<RoomSnapshot>> action, CancellationToken ct)
    {
        var snapshot = await action(http.User.GetRoomId(), http.User.GetPlayerName());
        await notifier.PublishAsync(snapshot, ct);
        return Results.Ok(snapshot);
    }
}
