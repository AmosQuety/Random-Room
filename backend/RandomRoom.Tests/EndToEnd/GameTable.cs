using System.Net.Http.Json;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>A room with every seat claimed and one bot per seat, the first being the host.</summary>
public sealed class GameTable : IAsyncDisposable
{
    public const string Pin = "1234";

    private static readonly string[] SeatNames =
        ["Amos", "Lydia", "James", "Jacob", "Sarah", "Judith", "Peter", "Grace", "Moses", "Esther", "David", "Ruth"];

    private readonly EndToEndFactory factory;
    private readonly List<Bot> bots = [];
    private string? latecomer;

    private GameTable(EndToEndFactory factory, string slug)
    {
        this.factory = factory;
        Slug = slug;
    }

    public string Slug { get; }

    public IReadOnlyList<Bot> Bots => bots;

    public Bot Host => bots[0];

    /// <summary>Opens a room for the strategy's game with every player signed in and listening.</summary>
    public static async Task<GameTable> OpenAsync(EndToEndFactory factory, IGameStrategy strategy, int playerCount)
    {
        var table = await CreateAsync(factory, strategy, playerCount);
        foreach (var name in SeatNames.Take(playerCount))
            await table.SeatAsync(name);
        return table;
    }

    /// <summary>Like OpenAsync, but the last player has not signed in yet; call SeatLatecomerAsync to bring them in.</summary>
    public static async Task<GameTable> OpenWithLatecomerAsync(EndToEndFactory factory, IGameStrategy strategy, int playerCount)
    {
        var table = await CreateAsync(factory, strategy, playerCount);
        foreach (var name in SeatNames.Take(playerCount - 1))
            await table.SeatAsync(name);
        table.latecomer = SeatNames[playerCount - 1];
        return table;
    }

    /// <summary>Signs in and connects the player who was left out by OpenWithLatecomerAsync.</summary>
    public async Task<Bot> SeatLatecomerAsync() =>
        await SeatAsync(latecomer ?? throw new InvalidOperationException("This table has no latecomer."));

    private static async Task<GameTable> CreateAsync(EndToEndFactory factory, IGameStrategy strategy, int playerCount)
    {
        var client = factory.CreateClient();
        var players = SeatNames.Take(playerCount).ToList();

        var response = await client.PostAsJsonAsync("/api/rooms",
            new CreateRoomHttpRequest("End to end", strategy.GameType, TestSetup.Of(strategy.Setup(playerCount)), players, players[0]));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreateRoomResult>())!;

        foreach (var invite in created.Invites)
            (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{invite.InviteToken}", new ClaimInviteRequest(Pin)))
                .EnsureSuccessStatusCode();

        return new GameTable(factory, created.Slug);
    }

    private async Task<Bot> SeatAsync(string name)
    {
        var bot = await Bot.JoinAsync(factory, Slug, name, Pin);
        await bot.ConnectAsync();
        lock (bots) bots.Add(bot);
        return bot;
    }

    /// <summary>Waits until every connected bot's latest snapshot satisfies the condition.</summary>
    public Task AllSeeAsync(Func<Snapshot, bool> condition, string description) =>
        Task.WhenAll(bots.Where(b => b.IsConnected).Select(b => b.WaitForAsync(condition, description)));

    /// <summary>Runs one task per bot at the same time, as separate players would act.</summary>
    public Task AllActAsync(Func<Bot, int, Task> act) =>
        Task.WhenAll(bots.Select((bot, index) => act(bot, index)));

    public async ValueTask DisposeAsync()
    {
        foreach (var bot in bots) await bot.DisposeAsync();
    }
}
