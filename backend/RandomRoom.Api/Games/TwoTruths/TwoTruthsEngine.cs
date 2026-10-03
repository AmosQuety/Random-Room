using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.TwoTruths;

public sealed class TwoTruthsData
{
    /// <summary>Storytellers in play order: everyone gets exactly one turn.</summary>
    public List<string> Order { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    public JsonElement? Result { get; set; }
}

public sealed record StatementSet(List<string> Statements, int Lie);

public sealed record VoteChoice(int Choice);

public sealed record TwoTruthsPreview(string Rule);

public sealed record TwoTruthsScore(string Player, int Score);

/// <summary>
/// What a viewer sees. The lie is in here only for the storyteller (MyLie) until the reveal; everyone else
/// gets the statements but never which one is false, and votes stay hidden until the reveal.
/// </summary>
public sealed record TwoTruthsPayload(
    string Phase,
    int Round,
    int TotalRounds,
    string? Storyteller,
    IReadOnlyList<string>? Statements,
    IReadOnlyDictionary<string, bool> Voted,
    int? MyVote,
    int? MyLie,
    JsonElement? Result,
    IReadOnlyList<TwoTruthsScore> Scoreboard);

/// <summary>
/// Each player takes a turn as storyteller: they write three statements and mark the lie, everyone else votes
/// for the one they think is false, then it reveals. Scoring: a correct guess scores 1, and the storyteller
/// scores 1 for every player they fooled. Actions: submit (storyteller), vote, reveal / next / skip (host).
/// </summary>
public sealed class TwoTruthsEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "two-truths";

    public const string Submitting = "submitting";
    public const string Voting = "voting";

    private const string StatementsKind = "statements";
    private const string VoteKind = "vote";
    private const int StatementCount = 3;
    private const int MaxStatementLength = 140;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Submitting),
        (Submitting, Voting),
        (Voting, Phases.Revealed),
        (Phases.Revealed, Submitting),
        (Phases.Revealed, Phases.Complete),
        (Submitting, Submitting),
        (Submitting, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 3;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct) => Task.CompletedTask;

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new TwoTruthsData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var players = await store.PlayerNamesAsync(roomId, ct);
        await store.InSessionAsync<TwoTruthsData>(sessionId, async state =>
        {
            state.Data.Order = random.Shuffle(players).ToList();
            state.Data.Scores = [];
            state.Phase = Guard.Move(state.Phase, Submitting);
            state.Round = 1;
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("submit" or "vote" or "reveal" or "next" or "skip"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Two Truths and a Lie has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        await store.InSessionAsync<TwoTruthsData>(sessionId, async state =>
        {
            switch (action)
            {
                case "submit": await SubmitAsync(sessionId, actor, payload, state, ct); break;
                case "vote": await VoteAsync(sessionId, actor, payload, state, players, ct); break;
                case "reveal": RequireHost(actor, host); await RevealAsync(sessionId, state, players, ct); break;
                case "next": RequireHost(actor, host); await NextAsync(state, Phases.Revealed, "Reveal the round before moving on.", ct); break;
                case "skip": RequireHost(actor, host); await NextAsync(state, Submitting, "Only a storyteller who has not submitted can be skipped.", ct); break;
            }
        }, ct);
    }

    private async Task SubmitAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<TwoTruthsData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Submitting, "The statements for this round are already in.");
        var storyteller = StorytellerOf(state);
        if (actor != storyteller)
            throw new RoomRuleException(RuleViolation.Forbidden, $"It is {storyteller}'s turn to write.");

        var set = ParseStatements(payload);
        await store.AddEntryAsync(sessionId, state.Round, StatementsKind, actor, set, "You have already submitted your statements.", ct);
        state.Phase = Guard.Move(state.Phase, Voting);
        await store.SaveStateAsync(state, ct);
    }

    private static StatementSet ParseStatements(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body
            || !body.TryGetProperty("statements", out var listElement) || listElement.ValueKind != JsonValueKind.Array)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Send three statements and the number of the lie.");

        var statements = listElement.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, "Statements must be text."))
            .ToList();
        if (statements.Count != StatementCount)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Write exactly {StatementCount} statements.");
        if (statements.Any(s => s.Length == 0))
            throw new RoomRuleException(RuleViolation.InvalidInput, "Every statement needs some text.");
        if (statements.Any(s => s.Length > MaxStatementLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Keep each statement under {MaxStatementLength} characters.");
        if (statements.Select(AnswerNormalizer.Normalize).Distinct().Count() != StatementCount)
            throw new RoomRuleException(RuleViolation.InvalidInput, "The three statements must be different.");

        var lie = AnswerJson.RequiredInt(body, "lie");
        if (lie is < 0 or >= StatementCount)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Mark which statement is the lie.");
        return new StatementSet(statements, lie);
    }

    private async Task VoteAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<TwoTruthsData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Voting, "Voting is not open right now.");
        if (actor == StorytellerOf(state))
            throw new RoomRuleException(RuleViolation.Forbidden, "The storyteller does not vote on their own statements.");

        var choice = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredInt(body, "choice") : -1;
        if (choice is < 0 or >= StatementCount)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Vote for one of the three statements.");

        await store.AddEntryAsync(sessionId, state.Round, VoteKind, actor, new VoteChoice(choice), "You have already voted.", ct);
        if (await store.CountEntriesAsync(sessionId, VoteKind, state.Round, ct) >= players.Count - 1)
            await RevealAsync(sessionId, state, players, ct);
    }

    private async Task RevealAsync(Guid sessionId, SessionState<TwoTruthsData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Voting, "There is nothing to reveal yet.");
        var storyteller = StorytellerOf(state);
        var set = (await store.EntriesAsync<StatementSet>(sessionId, StatementsKind, state.Round, ct)).Single().Value;
        var votes = await store.EntriesAsync<VoteChoice>(sessionId, VoteKind, state.Round, ct);

        var caught = votes.Where(v => v.Value.Choice == set.Lie).Select(v => v.Player).ToList();
        var fooled = votes.Where(v => v.Value.Choice != set.Lie).Select(v => v.Player).ToList();

        foreach (var player in caught) AddPoints(state, player, 1);
        AddPoints(state, storyteller, fooled.Count);

        var byStatement = Enumerable.Range(0, StatementCount)
            .Select(i => votes.Where(v => v.Value.Choice == i).Select(v => v.Player).ToList())
            .ToList();
        state.Data.Result = GameStore.ToElement(new { storyteller, statements = set.Statements, lie = set.Lie, votes = byStatement, caught, fooled });
        state.Phase = Guard.Move(state.Phase, Phases.Revealed);
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<TwoTruthsData> state, string requiredPhase, string wrongPhaseMessage, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, requiredPhase, wrongPhaseMessage);
        if (state.Round >= state.Data.Order.Count)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Submitting);
            state.Round++;
            state.Data.Result = null;
        }
        await store.SaveStateAsync(state, ct);
    }

    private static void AddPoints(SessionState<TwoTruthsData> state, string player, int points) =>
        state.Data.Scores[player] = state.Data.Scores.GetValueOrDefault(player) + points;

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    private static string StorytellerOf(SessionState<TwoTruthsData> state) => state.Data.Order[state.Round - 1];

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<TwoTruthsData>(sessionId, ct)).Phase == Phases.Complete;

    // ---- views ----

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<TwoTruthsData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var inRound = state.Round >= 1;
        var storyteller = inRound ? StorytellerOf(state) : null;

        var set = inRound ? (await store.EntriesAsync<StatementSet>(sessionId, StatementsKind, state.Round, ct)).FirstOrDefault()?.Value : null;
        var votes = inRound ? await store.EntriesAsync<VoteChoice>(sessionId, VoteKind, state.Round, ct) : [];
        var statementsShown = state.Phase is Voting or Phases.Revealed or Phases.Complete;

        return new TwoTruthsPayload(
            state.Phase,
            state.Round,
            state.Data.Order.Count == 0 ? players.Count : state.Data.Order.Count,
            storyteller,
            statementsShown ? set?.Statements : null,
            players.Where(p => p != storyteller).ToDictionary(p => p, p => votes.Any(v => v.Player == p)),
            viewer is null ? null : votes.FirstOrDefault(v => v.Player == viewer)?.Value.Choice,
            viewer is not null && viewer == storyteller ? set?.Lie : null,
            state.Phase is Phases.Revealed or Phases.Complete ? state.Data.Result : null,
            players.Select(p => new TwoTruthsScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new TwoTruthsPreview("Everyone takes a turn telling two truths and a lie."));
}
