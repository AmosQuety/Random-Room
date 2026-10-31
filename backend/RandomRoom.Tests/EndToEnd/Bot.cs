using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// One player as a real client behaves: signed in with a token, listening on the room hub, acting over HTTP.
/// A bot knows only what the server sent it. Snapshots can reach it by two routes (a push and the response to its
/// own action) in either order, so it keeps the newest by sequence, as the frontend does.
/// </summary>
public sealed class Bot : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly EndToEndFactory factory;
    private readonly HttpClient http;
    private readonly string token;
    private readonly SnapshotLog log;
    private HubConnection? hub;

    private Bot(EndToEndFactory factory, string name, string token, bool isHost)
    {
        this.factory = factory;
        this.token = token;
        Name = name;
        IsHost = isHost;
        log = new SnapshotLog(name);
        http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public string Name { get; }

    public bool IsHost { get; }

    public bool IsConnected => hub?.State == HubConnectionState.Connected;

    /// <summary>The newest snapshot by sequence, or null before the first one arrives.</summary>
    public Snapshot? Latest => log.Latest;

    /// <summary>Every snapshot this bot was ever sent, in arrival order, including ones it ignored as stale.</summary>
    public IReadOnlyList<Snapshot> Received => log.Received;

    public static async Task<Bot> JoinAsync(EndToEndFactory factory, string slug, string name, string pin)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/join", new JoinRequest(slug, name, pin));
        response.EnsureSuccessStatusCode();
        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>())!;
        return new Bot(factory, name, joined.Token, joined.IsHost);
    }

    // ---- connection ----

    /// <summary>Opens the hub and then fetches the room, as the frontend does after every (re)connect.</summary>
    public async Task ConnectAsync()
    {
        hub = BuildHub();
        hub.On<JsonElement>(SignalRRoomNotifier.SnapshotEvent, raw => Offer(new Snapshot(raw)));
        await hub.StartAsync();
        await RefreshAsync();
    }

    /// <summary>Drops the hub connection. Pushes sent while disconnected are never delivered.</summary>
    public async Task DisconnectAsync()
    {
        if (hub is null) return;
        await hub.DisposeAsync();
        hub = null;
    }

    public async Task RefreshAsync()
    {
        var response = await http.GetAsync("/api/room");
        response.EnsureSuccessStatusCode();
        Offer(new Snapshot(await response.Content.ReadFromJsonAsync<JsonElement>()));
    }

    private HubConnection BuildHub() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/room"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.WebSockets;
                // Like a browser, which cannot set headers on a WebSocket upgrade: the token travels in the query string.
                options.WebSocketFactory = async (context, ct) =>
                    await factory.Server.CreateWebSocketClient().ConnectAsync(WithAccessToken(context.Uri), ct);
            })
            .Build();

    private Uri WithAccessToken(Uri uri) =>
        new UriBuilder(uri) { Query = $"{uri.Query.TrimStart('?')}&access_token={Uri.EscapeDataString(token)}" }.Uri;

    // ---- snapshots ----

    public void Offer(Snapshot snapshot) => log.Offer(snapshot);

    public Task<Snapshot> WaitForAsync(Func<Snapshot, bool> condition, string description, TimeSpan? timeout = null) =>
        log.WaitForAsync(condition, description, timeout);

    // ---- acting ----

    /// <summary>Sends a game action. Throws when the server refuses it.</summary>
    public async Task<Snapshot> ActAsync(string action, object? payload = null) =>
        await RequireAcceptedAsync(action, await PostAsync("/api/room/action", new { action, payload }));

    /// <summary>Sends a game action and reports the status instead of throwing, for races where a refusal is a valid outcome.</summary>
    public async Task<HttpStatusCode> TryActAsync(string action, object? payload = null) =>
        await TryAcceptAsync(await PostAsync("/api/room/action", new { action, payload }));

    public Task<Snapshot> StartGameAsync() => ControlAsync("start", "/api/room/session/start");

    public Task<Snapshot> EndGameAsync() => ControlAsync("end game", "/api/room/session/end");

    public async Task<HttpStatusCode> TryEndGameAsync() =>
        await TryAcceptAsync(await PostAsync("/api/room/session/end", null));

    public Task<Snapshot> StartNewGameAsync() => ControlAsync("start new game", "/api/room/session/new");

    private async Task<Snapshot> ControlAsync(string label, string path) =>
        await RequireAcceptedAsync(label, await PostAsync(path, null));

    private Task<HttpResponseMessage> PostAsync(string path, object? body) =>
        http.PostAsync(path, body is null ? null : JsonContent.Create(body, options: Web));

    private async Task<Snapshot> RequireAcceptedAsync(string label, HttpResponseMessage response)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new BotActionException(Name, label, response.StatusCode, await response.Content.ReadAsStringAsync());

            var snapshot = new Snapshot(await response.Content.ReadFromJsonAsync<JsonElement>());
            Offer(snapshot);
            return snapshot;
        }
    }

    private async Task<HttpStatusCode> TryAcceptAsync(HttpResponseMessage response)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                Offer(new Snapshot(await response.Content.ReadFromJsonAsync<JsonElement>()));
            return response.StatusCode;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        http.Dispose();
    }
}
