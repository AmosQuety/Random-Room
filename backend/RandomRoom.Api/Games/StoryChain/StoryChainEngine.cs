using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.StoryChain;

/// <summary>What differs between the story games: how a turn is written and how long the story runs.</summary>
public interface IChainRules
{
    string GameType { get; }

    /// <summary>Name of the embedded bank of story openers.</summary>
    string OpenerBank { get; }

    int MinLength { get; }
    int MaxLength { get; }
    int DefaultLength { get; }

    /// <summary>What the player whose turn it is may write, for the labels around the input. Zero-based turn number.</summary>
    string PrefixFor(int turn);

    /// <summary>Checks and normalises one contribution. Throws InvalidInput.</summary>
    string Validate(string text);
}

public sealed record ChainSetup(List<string> Openers, bool UseBuiltIn, int Length);

/// <summary>One turn of the story. Prefix is the lead-in the server added for that turn (empty in games without one).</summary>
public sealed record ChainEntry(string Player, string Text, string Prefix);

public sealed class ChainData
{
    public string Opener { get; set; } = "";

    public List<ChainEntry> Entries { get; set; } = [];

    /// <summary>Turns used so far, including skipped ones. The next turn belongs to Turn modulo the player count.</summary>
    public int Turn { get; set; }
}

public sealed record ChainScore(string Player, int Score);

public sealed record ChainPreview(string Rule);

/// <summary>Everything is public by design: the story is written in the open, one turn at a time.</summary>
public sealed record ChainPayload(
    string Phase,
    string Opener,
    IReadOnlyList<ChainEntry> Entries,
    int Turn,
    int TotalTurns,
    string? CurrentPlayer,
    string? Prefix,
    IReadOnlyList<ChainScore> Scoreboard);

/// <summary>
/// A story written turn by turn, in a fixed order. The server decides whose turn it is and checks each contribution
/// against the game's rules. The story ends after the set number of turns, or when the host ends it.
/// Scoring: 1 point per contribution, so the scoreboard is a count of turns taken.
/// Actions: add (the player whose turn it is), skip / end (host).
/// </summary>
public sealed class StoryChainEngine(IChainRules rules, GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Writing = "writing";
    public const int MaxOpenerLength = 140;
    public const int MaxOpeners = 30;

    private static readonly PhaseGuard Guard = new((Phases.Lobby, Writing), (Writing, Phases.Complete));

    public string GameType => rules.GameType;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var openers = SetupJson.OptionalArray(setup, "openers", MaxOpeners, "Story openers")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Opener {i + 1} must be text."))
            .ToList();
        foreach (var (opener, i) in openers.Select((o, i) => (o, i)))
        {
            if (opener.Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Opener {i + 1} cannot be empty.");
            if (opener.Length > MaxOpenerLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Opener {i + 1} is too long (at most {MaxOpenerLength} characters).");
        }

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", openers.Count == 0);
        if (!useBuiltIn && openers.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Add at least one opener, or use the built-in ones.");

        var length = SetupJson.OptionalInt(setup, "length", rules.MinLength, rules.MaxLength, "The story length") ?? rules.DefaultLength;
        store.AddSetup(roomId, new ChainSetup(openers, useBuiltIn, length));
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new ChainData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<ChainSetup>(roomId, ct);
        await store.InSessionAsync<ChainData>(sessionId, async state =>
        {
            var pool = setup.Openers.Concat(setup.UseBuiltIn ? ContentBank.Load<string>(rules.OpenerBank) : []).ToList();
            state.Data = new ChainData { Opener = pool[random.PickIndex(pool.Count)] };
            state.Phase = Guard.Move(state.Phase, Writing);
            state.Round = 1;
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("add" or "skip" or "end"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"This game has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        var setup = await store.GetSetupAsync<ChainSetup>(roomId, ct);
        await store.InSessionAsync<ChainData>(sessionId, async state =>
        {
            switch (action)
            {
                case "add": await AddAsync(actor, payload, state, players, setup, ct); break;
                case "skip": RequireHost(actor, host); await SkipAsync(state, setup, ct); break;
                case "end": RequireHost(actor, host); await EndAsync(state, ct); break;
            }
        }, ct);
    }

    private async Task AddAsync(string actor, JsonElement? payload, SessionState<ChainData> state, IReadOnlyList<string> players, ChainSetup setup, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Writing, "The story is finished.");
        var current = CurrentPlayer(state.Data, players);
        if (actor != current)
            throw new RoomRuleException(RuleViolation.Forbidden, $"It is {current}'s turn.");

        var raw = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredText(body, "text", 400) : throw new RoomRuleException(RuleViolation.InvalidInput, "Write your part of the story.");
        var text = rules.Validate(raw);

        state.Data.Entries.Add(new ChainEntry(actor, text, rules.PrefixFor(state.Data.Turn)));
        Advance(state, setup);
        await store.SaveStateAsync(state, ct);
    }

    private async Task SkipAsync(SessionState<ChainData> state, ChainSetup setup, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Writing, "The story is finished.");
        Advance(state, setup);
        await store.SaveStateAsync(state, ct);
    }

    private async Task EndAsync(SessionState<ChainData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Writing, "The story is already finished.");
        state.Phase = Guard.Move(state.Phase, Phases.Complete);
        await store.SaveStateAsync(state, ct);
    }

    private void Advance(SessionState<ChainData> state, ChainSetup setup)
    {
        state.Data.Turn++;
        if (state.Data.Turn >= setup.Length)
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
    }

    private static string CurrentPlayer(ChainData data, IReadOnlyList<string> players) => players[data.Turn % players.Count];

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<ChainData>(sessionId, ct)).Phase == Phases.Complete;

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<ChainData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var setup = await store.GetSetupAsync<ChainSetup>(roomId, ct);
        var writing = state.Phase == Writing;
        return new ChainPayload(
            state.Phase,
            state.Data.Opener,
            state.Data.Entries,
            state.Data.Turn,
            setup.Length,
            writing ? CurrentPlayer(state.Data, players) : null,
            writing ? rules.PrefixFor(state.Data.Turn) : null,
            players.Select(p => new ChainScore(p, state.Data.Entries.Count(e => e.Player == p))).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new ChainPreview("Take turns adding to a story, one piece at a time."));
}
