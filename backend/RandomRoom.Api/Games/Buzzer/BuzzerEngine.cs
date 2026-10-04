using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Buzzer;

public sealed record BuzzerSetup(List<string> Prompts, bool UseBuiltIn, int Rounds);

public sealed class BuzzerData
{
    /// <summary>The prompts for this session in play order, chosen when it starts.</summary>
    public List<string> Prompts { get; set; } = [];

    /// <summary>Players who buzzed and were judged wrong this round. They cannot buzz again until the next round.</summary>
    public List<string> LockedOut { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    /// <summary>Who won the round, once it is resolved. Null for a skipped or fully missed round.</summary>
    public string? Winner { get; set; }

    /// <summary>
    /// Every point here is the host's judgement, so each one is listed. The host does not play Buzzer, so this is about
    /// transparency rather than fairness, and a host who plays is not possible here.
    /// </summary>
    public List<HostScoreNote> HostScoring { get; set; } = [];
}

public sealed record BuzzerScore(string Player, int Score);

public sealed record BuzzerPreview(string Rule);

/// <summary>Everything here is public: a buzz is a visible event, not a secret.</summary>
public sealed record BuzzerPayload(
    string Phase,
    int Round,
    int TotalRounds,
    string? Prompt,
    string? Buzzed,
    IReadOnlyList<string> LockedOut,
    string? Winner,
    IReadOnlyList<BuzzerScore> Scoreboard,
    IReadOnlyList<HostScoreNote> HostScoring);

/// <summary>
/// Quick-fire rounds. The host reads a prompt, opens the buzzers, and the first player to buzz answers out loud;
/// the host judges. Right scores 1 and ends the round. Wrong locks that player out and reopens the buzzers.
/// The first buzz is decided by one atomic database UPDATE, in the order the server receives requests.
/// The host judges, so the host does not buzz and the game needs 3 players.
/// Actions: open / correct / wrong / skip / next (host), buzz (everyone else).
/// </summary>
public sealed class BuzzerEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "buzzer";

    public const string Waiting = "waiting";
    public const string Open = "open";
    public const string Resolved = "resolved";

    public const int MaxPrompts = 30;
    public const int MaxPromptLength = 140;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Waiting),
        (Waiting, Open),
        (Open, Resolved),
        (Waiting, Resolved),
        (Resolved, Waiting),
        (Resolved, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 3;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var prompts = SetupJson.OptionalArray(setup, "prompts", MaxPrompts, "Prompts")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Prompt {i + 1} must be text."))
            .ToList();
        foreach (var (prompt, i) in prompts.Select((p, i) => (p, i)))
        {
            if (prompt.Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Prompt {i + 1} cannot be empty.");
            if (prompt.Length > MaxPromptLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Prompt {i + 1} is too long (at most {MaxPromptLength} characters).");
        }

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", prompts.Count == 0);
        if (!useBuiltIn && prompts.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Add at least one prompt, or use the built-in ones.");

        var rounds = SetupJson.OptionalInt(setup, "rounds", 1, MaxPrompts * 2, "Number of rounds") ?? 8;
        store.AddSetup(roomId, new BuzzerSetup(prompts, useBuiltIn, rounds));
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new BuzzerData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<BuzzerSetup>(roomId, ct);
        await store.InSessionAsync<BuzzerData>(sessionId, async state =>
        {
            var pool = setup.Prompts.Concat(setup.UseBuiltIn ? ContentBank.Load<string>("buzzer") : []).ToList();
            state.Data.Prompts = random.Shuffle(pool).Take(setup.Rounds).ToList();
            state.Data.Scores = [];
            state.Phase = Guard.Move(state.Phase, Waiting);
            state.Round = 1;
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("buzz" or "open" or "correct" or "wrong" or "skip" or "next"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Buzzer has no '{action}' action.");

        var host = await store.HostOfAsync(roomId, ct);
        if (action == "buzz")
        {
            await BuzzAsync(sessionId, actor, host, ct);
            return;
        }

        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        await store.InSessionAsync<BuzzerData>(sessionId, async state =>
        {
            switch (action)
            {
                case "open": await OpenAsync(state, ct); break;
                case "correct": await CorrectAsync(state, ct); break;
                case "wrong": await WrongAsync(state, players, host, ct); break;
                case "skip": await SkipAsync(state, ct); break;
                case "next": await NextAsync(state, ct); break;
            }
        }, ct);
    }

    /// <summary>
    /// Deliberately not under the session row lock: the atomic claim is the arbiter, so a buzz never waits behind
    /// another action and the winner is whoever's UPDATE the database applies first.
    /// </summary>
    private async Task BuzzAsync(Guid sessionId, string actor, string host, CancellationToken ct)
    {
        if (actor == host)
            throw new RoomRuleException(RuleViolation.Forbidden, "The host judges, so the host does not buzz.");

        var state = await store.LoadStateAsync<BuzzerData>(sessionId, ct);
        PhaseGuard.Require(state.Phase, Open, "The buzzers are not open.");
        if (state.Data.LockedOut.Contains(actor))
            throw new RoomRuleException(RuleViolation.Forbidden, "You already answered this round.");

        if (!await store.TryClaimInPhaseAsync(sessionId, state.Round, Open, actor, ct))
            throw new RoomRuleException(RuleViolation.Conflict, "Someone buzzed first.");
    }

    private async Task OpenAsync(SessionState<BuzzerData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Waiting, "The buzzers are already open.");
        state.Phase = Guard.Move(state.Phase, Open);
        state.Row.Claimant = null;
        await store.SaveStateAsync(state, ct);
    }

    private async Task CorrectAsync(SessionState<BuzzerData> state, CancellationToken ct)
    {
        var winner = RequireBuzzed(state);
        state.Data.Scores[winner] = state.Data.Scores.GetValueOrDefault(winner) + 1;
        state.Data.HostScoring.Add(new HostScoreNote(state.Round, winner, 1, "The host judged the answer correct"));
        state.Data.Winner = winner;
        state.Phase = Guard.Move(state.Phase, Resolved);
        await store.SaveStateAsync(state, ct);
    }

    private async Task WrongAsync(SessionState<BuzzerData> state, IReadOnlyList<string> players, string host, CancellationToken ct)
    {
        var loser = RequireBuzzed(state);
        state.Data.LockedOut.Add(loser);
        state.Row.Claimant = null;
        if (players.Where(p => p != host).All(state.Data.LockedOut.Contains))
        {
            state.Data.Winner = null;
            state.Phase = Guard.Move(state.Phase, Resolved);
        }
        await store.SaveStateAsync(state, ct);
    }

    private async Task SkipAsync(SessionState<BuzzerData> state, CancellationToken ct)
    {
        if (state.Phase is not (Waiting or Open))
            throw new RoomRuleException(RuleViolation.Conflict, "This round is already over.");
        state.Data.Winner = null;
        state.Row.Claimant = null;
        state.Phase = Guard.Move(state.Phase, Resolved);
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<BuzzerData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Resolved, "Finish this round before moving on.");
        if (state.Round >= state.Data.Prompts.Count)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Waiting);
            state.Round++;
            state.Data.LockedOut = [];
            state.Data.Winner = null;
            state.Row.Claimant = null;
        }
        await store.SaveStateAsync(state, ct);
    }

    private static string RequireBuzzed(SessionState<BuzzerData> state)
    {
        PhaseGuard.Require(state.Phase, Open, "The buzzers are not open.");
        return state.Row.Claimant ?? throw new RoomRuleException(RuleViolation.Conflict, "Nobody has buzzed yet.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<BuzzerData>(sessionId, ct)).Phase == Phases.Complete;

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<BuzzerData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var playing = state.Round >= 1 && state.Phase != Phases.Complete;
        return new BuzzerPayload(
            state.Phase,
            state.Round,
            state.Data.Prompts.Count,
            playing ? state.Data.Prompts[state.Round - 1] : null,
            state.Phase == Open ? state.Row.Claimant : null,
            state.Data.LockedOut,
            state.Phase == Resolved ? state.Data.Winner : null,
            players.Select(p => new BuzzerScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList(),
            state.Data.HostScoring);
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new BuzzerPreview("Quick-fire rounds: be first to buzz, then answer out loud."));
}
