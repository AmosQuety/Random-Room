using System.Text.Json;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Endpoints;

public sealed record JoinRequest(string? RoomSlug, string? Player, string? Pin);

public sealed record JoinResponse(string Token, string Player, string RoomSlug, bool IsHost);

public sealed record CreateRoomHttpRequest(string Title, string GameType, JsonElement Setup, List<string> Players, string HostPlayer);

public sealed record ClaimInviteRequest(string Pin);

public sealed record ClaimInviteResponse(string Player);

public sealed record RecoverHostRequest(string? Code);

public sealed record RecoverHostResponse(string Player, string InviteToken, string RecoveryCode);

public sealed record RecoveryCodeResponse(string RecoveryCode);

public sealed record ActionRequest(string Action, JsonElement? Payload);

public sealed record AppConfig(int RetentionDays);

public static class RoomEndpoints
{
    public const string JoinRateLimitPolicy = "join";
    public const string RoomAdminRateLimitPolicy = "room-admin";

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/join", Join).RequireRateLimiting(JoinRateLimitPolicy);

        // What the app needs to know before any room exists, so the create screen can say how long rooms are kept.
        api.MapGet("/config", (Microsoft.Extensions.Options.IOptions<RoomOptions> room) =>
            Results.Ok(new AppConfig(room.Value.RetentionDays)));

        var rooms = api.MapGroup("/rooms");
        rooms.MapPost("/", CreateRoom).RequireRateLimiting(RoomAdminRateLimitPolicy);
        rooms.MapGet("/{slug}", GetPreview);
        rooms.MapPost("/{slug}/claim/{token}", ClaimInvite).RequireRateLimiting(RoomAdminRateLimitPolicy);
        // Guessing a recovery code is the same kind of attack as guessing a PIN, so it shares the join throttle.
        rooms.MapPost("/{slug}/recover", RecoverHost).RequireRateLimiting(JoinRateLimitPolicy);

        var room = api.MapGroup("/room").RequireAuthorization();
        room.MapGet("/", (GameSessionService rooms, HttpContext http, CancellationToken ct) =>
            rooms.GetSnapshotForAsync(http.User.GetRoomId(), http.User.GetPlayerName(), ct));
        room.MapPost("/action", (HttpContext http, ActionRequest request, GameSessionService rooms, RoomBroadcaster broadcaster, CancellationToken ct) =>
            Act(http, broadcaster, (roomId, player) => rooms.PerformActionAsync(roomId, player, request.Action, request.Payload, ct), ct));
        room.MapPost("/recovery-code", MakeRecoveryCode).RequireRateLimiting(RoomAdminRateLimitPolicy);
        room.MapDelete("/", DeleteRoom).RequireRateLimiting(RoomAdminRateLimitPolicy);
        room.MapPost("/players/{name}/reset-pin", ResetPin).RequireRateLimiting(RoomAdminRateLimitPolicy);
        room.MapPost("/session/start", (HttpContext http, GameSessionService rooms, RoomBroadcaster broadcaster, CancellationToken ct) =>
            Act(http, broadcaster, (roomId, player) => rooms.StartSessionAsync(roomId, player, ct), ct));
        room.MapPost("/session/end", (HttpContext http, GameSessionService rooms, RoomBroadcaster broadcaster, CancellationToken ct) =>
            Act(http, broadcaster, (roomId, player) => rooms.EndSessionAsync(roomId, player, ct), ct));
        room.MapPost("/session/new", (HttpContext http, GameSessionService rooms, RoomBroadcaster broadcaster, CancellationToken ct) =>
            Act(http, broadcaster, (roomId, player) => rooms.StartNewSessionAsync(roomId, player, ct), ct));
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
            new CreateRoomRequest(request.Title, request.GameType, request.Setup, request.Players, request.HostPlayer), ct);
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

    private static async Task<IResult> RecoverHost(
        string slug, RecoverHostRequest request, RoomAdminService admin, RoomBroadcaster broadcaster, CancellationToken ct)
    {
        var recovery = await admin.RecoverHostSeatAsync(slug, request.Code ?? "", ct);
        // The old device is signed out by the seat's new token version; also cut its live connection and tell the room.
        await broadcaster.RevokeSeatAsync(recovery.RoomId, recovery.Player, ct);
        await broadcaster.PublishAsync(recovery.RoomId, ct);
        return Results.Ok(new RecoverHostResponse(recovery.Player, recovery.InviteToken, recovery.NewRecoveryCode));
    }

    private static async Task<IResult> MakeRecoveryCode(HttpContext http, RoomAdminService admin, CancellationToken ct) =>
        Results.Ok(new RecoveryCodeResponse(await admin.MakeRecoveryCodeAsync(http.User.GetRoomId(), http.User.GetPlayerName(), ct)));

    private static async Task<IResult> DeleteRoom(HttpContext http, RoomAdminService admin, RoomBroadcaster broadcaster, CancellationToken ct)
    {
        var roomId = http.User.GetRoomId();
        await admin.DeleteRoomAsync(roomId, http.User.GetPlayerName(), ct);
        await broadcaster.AnnounceRoomDeletedAsync(roomId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPin(
        string name, HttpContext http, RoomAdminService admin, RoomBroadcaster broadcaster, CancellationToken ct)
    {
        var roomId = http.User.GetRoomId();
        var invite = await admin.ResetSeatAsync(roomId, http.User.GetPlayerName(), name, ct);
        await broadcaster.RevokeSeatAsync(roomId, name, ct);
        await broadcaster.PublishAsync(roomId, ct);
        return Results.Ok(invite);
    }

    private static async Task<IResult> Act(
        HttpContext http, RoomBroadcaster broadcaster, Func<Guid, string, Task<RoomSnapshot>> action, CancellationToken ct)
    {
        var roomId = http.User.GetRoomId();
        var snapshot = await action(roomId, http.User.GetPlayerName());
        await broadcaster.PublishAsync(roomId, ct);
        return Results.Ok(snapshot);
    }
}
