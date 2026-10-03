using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Bingo;

public sealed record BingoSetup(List<string> Items, bool UseBuiltIn);

public sealed class BingoData
{
    /// <summary>Every item in the order it will be called. Never sent: it would say what is coming next.</summary>
    public List<string> Order { get; set; } = [];

    public int CalledCount { get; set; }

    /// <summary>Each player's own card, 25 cells with the free centre empty. Only ever sent to its owner.</summary>
    public Dictionary<string, List<string>> Cards { get; set; } = [];

    /// <summary>The cells each player has marked. Cell indexes, never sent for anyone but the viewer.</summary>
    public Dictionary<string, List<int>> Marks { get; set; } = [];

    public string? Winner { get; set; }

    public int[]? WinningLine { get; set; }
}

public sealed record BingoScore(string Player, int Score);

public sealed record BingoPreview(string Rule);

/// <summary>What a viewer sees: what has been called (public), their own card and marks, and the result.</summary>
public sealed record BingoPayload(
    string Phase,
    IReadOnlyList<string> Called,
    int PoolSize,
    IReadOnlyList<string>? MyCard,
    IReadOnlyList<bool>? MyMarks,
    string? Winner,
    IReadOnlyList<int>? WinningLine,
    IReadOnlyList<BingoScore> Scoreboard);

/// <summary>
/// Everyone gets their own randomly laid out 5x5 card. The host calls the next item (the server draws it from
/// a pre-shuffled order, so the call cannot be steered), players mark called items on their card, and the first
/// valid bingo claim wins. The server checks that a marked cell was really called and that a claimed line is real.
/// Actions: call / end (host), mark, bingo.
/// </summary>
public sealed class BingoEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "bingo";

    public const string Calling = "calling";

    public const int MaxItems = 75;
    public const int MaxItemLength = 40;
    /// <summary>One more than a card holds, so cards can differ from each other.</summary>
    public const int MinPool = BingoCard.ItemsPerCard + 1;

    private const int CardAttempts = 200;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Calling),
        (Calling, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 2;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var items = SetupJson.OptionalArray(setup, "items", MaxItems, "Bingo items")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Item {i + 1} must be text."))
            .ToList();
        foreach (var (item, i) in items.Select((s, i) => (s, i)))
        {
            if (item.Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Item {i + 1} cannot be empty.");
            if (item.Length > MaxItemLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Item {i + 1} is too long (at most {MaxItemLength} characters).");
        }
        if (items.Select(AnswerNormalizer.Normalize).Distinct().Count() != items.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Each bingo item must be different.");

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", items.Count == 0);
        if (Pool(new BingoSetup(items, useBuiltIn)).Count < MinPool)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Bingo needs at least {MinPool} different items, or use the built-in ones.");

        store.AddSetup(roomId, new BingoSetup(items, useBuiltIn));
        return Task.CompletedTask;
    }

    private static List<string> Pool(BingoSetup setup)
    {
        var pool = setup.Items.ToList();
        if (setup.UseBuiltIn)
            pool.AddRange(ContentBank.Load<string>("bingo").Where(b => !pool.Any(p => AnswerNormalizer.Normalize(p) == AnswerNormalizer.Normalize(b))));
        return pool;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new BingoData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<BingoSetup>(roomId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        await store.InSessionAsync<BingoData>(sessionId, async state =>
        {
            var pool = Pool(setup);
            state.Data.Order = random.Shuffle(pool).ToList();
            state.Data.CalledCount = 0;
            state.Data.Cards = DealCards(pool, players);
            state.Data.Marks = players.ToDictionary(p => p, _ => new List<int> { BingoCard.FreeCell });
            state.Data.Winner = null;
            state.Data.WinningLine = null;
            state.Phase = Guard.Move(state.Phase, Calling);
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    /// <summary>One card per player, each laid out differently: a repeated layout is dealt again.</summary>
    private Dictionary<string, List<string>> DealCards(List<string> pool, IReadOnlyList<string> players)
    {
        var cards = new Dictionary<string, List<string>>();
        var layouts = new HashSet<string>();
        foreach (var player in players)
        {
            for (var attempt = 0; ; attempt++)
            {
                if (attempt == CardAttempts) throw new InvalidOperationException("Could not deal a distinct bingo card.");
                var card = BingoCard.Lay(random.Shuffle(pool).Take(BingoCard.ItemsPerCard));
                if (!layouts.Add(string.Join('\u001f', card))) continue;
                cards[player] = card;
                break;
            }
        }
        return cards;
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("call" or "end" or "mark" or "bingo"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Bingo has no '{action}' action.");

        var host = await store.HostOfAsync(roomId, ct);
        await store.InSessionAsync<BingoData>(sessionId, async state =>
        {
            switch (action)
            {
                case "call": RequireHost(actor, host); await CallAsync(state, ct); break;
                case "end": RequireHost(actor, host); await EndAsync(state, ct); break;
                case "mark": await MarkAsync(actor, payload, state, ct); break;
                case "bingo": await ClaimAsync(actor, state, ct); break;
            }
        }, ct);
    }

    private async Task CallAsync(SessionState<BingoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Calling, "The game is over.");
        if (state.Data.CalledCount >= state.Data.Order.Count)
            throw new RoomRuleException(RuleViolation.Conflict, "Every item has been called.");
        state.Data.CalledCount++;
        await store.SaveStateAsync(state, ct);
    }

    private async Task EndAsync(SessionState<BingoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Calling, "The game is already over.");
        state.Phase = Guard.Move(state.Phase, Phases.Complete);
        await store.SaveStateAsync(state, ct);
    }

    private async Task MarkAsync(string actor, JsonElement? payload, SessionState<BingoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Calling, "The game is over.");
        var cell = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredInt(body, "cell") : throw new RoomRuleException(RuleViolation.InvalidInput, "Pick a cell to mark.");
        if (cell < 0 || cell >= BingoCard.Cells)
            throw new RoomRuleException(RuleViolation.InvalidInput, "That cell is not on the card.");

        var marks = state.Data.Marks[actor];
        if (marks.Contains(cell)) return;

        var item = state.Data.Cards[actor][cell];
        if (!Called(state.Data).Contains(item, StringComparer.Ordinal))
            throw new RoomRuleException(RuleViolation.Conflict, "That item has not been called yet.");

        marks.Add(cell);
        await store.SaveStateAsync(state, ct);
    }

    private async Task ClaimAsync(string actor, SessionState<BingoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Calling, "Someone already won, or the game is over.");
        var line = BingoCard.WinningLine(MarkFlags(state.Data.Marks[actor]))
            ?? throw new RoomRuleException(RuleViolation.Conflict, "That is not a bingo yet.");

        state.Data.Winner = actor;
        state.Data.WinningLine = line;
        state.Phase = Guard.Move(state.Phase, Phases.Complete);
        await store.SaveStateAsync(state, ct);
    }

    private static IReadOnlyList<string> Called(BingoData data) => data.Order.Take(data.CalledCount).ToList();

    private static bool[] MarkFlags(IEnumerable<int> marks)
    {
        var flags = new bool[BingoCard.Cells];
        foreach (var cell in marks) flags[cell] = true;
        return flags;
    }

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<BingoData>(sessionId, ct)).Phase == Phases.Complete;

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<BingoData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var hasCard = viewer is not null && state.Data.Cards.ContainsKey(viewer);
        return new BingoPayload(
            state.Phase,
            Called(state.Data),
            state.Data.Order.Count,
            hasCard ? state.Data.Cards[viewer!] : null,
            hasCard ? MarkFlags(state.Data.Marks[viewer!]) : null,
            state.Data.Winner,
            state.Data.WinningLine,
            players.Select(p => new BingoScore(p, p == state.Data.Winner ? 1 : 0)).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new BingoPreview("Everyone gets their own bingo card. Mark what is called and shout bingo first."));
}
