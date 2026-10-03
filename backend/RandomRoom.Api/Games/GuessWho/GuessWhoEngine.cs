using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.GuessWho;

public sealed class GuessWhoData
{
    /// <summary>Authors in the order their facts are played. Never sent to clients: it would give the answers away.</summary>
    public List<string> Order { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    public JsonElement? Result { get; set; }
}

public sealed record Fact(string Text);

public sealed record Guess(string Player);

public sealed record GuessWhoPreview(string Rule);

public sealed record GuessWhoScore(string Player, int Score);

/// <summary>
/// What a viewer sees. Authorship of a fact is in here only for its own author (IsMine) until that fact is
/// revealed, and the play order is never included. Guesses stay hidden until the reveal.
/// </summary>
public sealed record GuessWhoPayload(
    string Phase,
    int Round,
    int TotalRounds,
    string? Text,
    bool IsMine,
    IReadOnlyDictionary<string, bool> Submitted,
    string? MyFact,
    int GuessCount,
    int GuessesNeeded,
    string? MyGuess,
    JsonElement? Result,
    IReadOnlyList<GuessWhoScore> Scoreboard);

/// <summary>
/// Everyone writes one fact about themselves. Then the facts come up one at a time, in random order, and
/// everyone else guesses who wrote it. Scoring: a correct guess scores 1. Text only, no uploads.
/// Actions: submit, guess, begin / reveal / next (host).
/// </summary>
public sealed class GuessWhoEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "guess-who";

    public const string Submitting = "submitting";
    public const string Voting = "voting";

    private const string FactKind = "fact";
    private const string GuessKind = "guess";
    private const int MaxFactLength = 200;
    private const int MinFactsToBegin = 2;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Submitting),
        (Submitting, Voting),
        (Voting, Phases.Revealed),
        (Phases.Revealed, Voting),
        (Phases.Revealed, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 3;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct) => Task.CompletedTask;

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new GuessWhoData());
        return Task.CompletedTask;
    }

    public Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        store.InSessionAsync<GuessWhoData>(sessionId, async state =>
        {
            state.Phase = Guard.Move(state.Phase, Submitting);
            await store.SaveStateAsync(state, ct);
        }, ct);

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("submit" or "guess" or "begin" or "reveal" or "next"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Guess Who has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        await store.InSessionAsync<GuessWhoData>(sessionId, async state =>
        {
            switch (action)
            {
                case "submit": await SubmitAsync(sessionId, actor, payload, state, players, ct); break;
                case "guess": await GuessAsync(sessionId, actor, payload, state, players, ct); break;
                case "begin": RequireHost(actor, host); await BeginAsync(sessionId, state, ct); break;
                case "reveal": RequireHost(actor, host); await RevealAsync(sessionId, state, ct); break;
                case "next": RequireHost(actor, host); await NextAsync(state, ct); break;
            }
        }, ct);
    }

    private async Task SubmitAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<GuessWhoData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Submitting, "Facts are no longer being collected.");
        var text = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredText(body, "text", MaxFactLength) : throw new RoomRuleException(RuleViolation.InvalidInput, "Write a fact about yourself.");

        await store.AddEntryAsync(sessionId, 0, FactKind, actor, new Fact(text), "You have already submitted your fact.", ct);
        if (await store.CountEntriesAsync(sessionId, FactKind, 0, ct) >= players.Count)
            await BeginAsync(sessionId, state, ct);
    }

    private async Task BeginAsync(Guid sessionId, SessionState<GuessWhoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Submitting, "The game has already begun.");
        var facts = await store.EntriesAsync<Fact>(sessionId, FactKind, 0, ct);
        if (facts.Count < MinFactsToBegin)
            throw new RoomRuleException(RuleViolation.Conflict, $"At least {MinFactsToBegin} facts are needed to begin.");

        state.Data.Order = random.Shuffle(facts.Select(f => f.Player)).ToList();
        state.Data.Scores = [];
        state.Phase = Guard.Move(state.Phase, Voting);
        state.Round = 1;
        await store.SaveStateAsync(state, ct);
    }

    private async Task GuessAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<GuessWhoData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Voting, "Guessing is not open right now.");
        if (actor == AuthorOf(state))
            throw new RoomRuleException(RuleViolation.Forbidden, "You wrote this one, so you do not guess.");

        var guessed = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredText(body, "player", 40) : throw new RoomRuleException(RuleViolation.InvalidInput, "Guess one of the players.");
        if (guessed == actor)
            throw new RoomRuleException(RuleViolation.InvalidInput, "You cannot guess yourself.");
        if (!players.Contains(guessed, StringComparer.Ordinal))
            throw new RoomRuleException(RuleViolation.InvalidInput, "Guess one of the players in this room.");

        await store.AddEntryAsync(sessionId, state.Round, GuessKind, actor, new Guess(guessed), "You have already guessed.", ct);
        if (await store.CountEntriesAsync(sessionId, GuessKind, state.Round, ct) >= players.Count - 1)
            await RevealAsync(sessionId, state, ct);
    }

    private async Task RevealAsync(Guid sessionId, SessionState<GuessWhoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Voting, "There is nothing to reveal yet.");
        var author = AuthorOf(state);
        var fact = (await store.EntriesAsync<Fact>(sessionId, FactKind, 0, ct)).Single(f => f.Player == author).Value;
        var guesses = await store.EntriesAsync<Guess>(sessionId, GuessKind, state.Round, ct);

        var correct = guesses.Where(g => g.Value.Player == author).Select(g => g.Player).ToList();
        foreach (var player in correct)
            state.Data.Scores[player] = state.Data.Scores.GetValueOrDefault(player) + 1;

        state.Data.Result = GameStore.ToElement(new
        {
            author,
            text = fact.Text,
            guesses = guesses.Select(g => new { player = g.Player, guessed = g.Value.Player }).ToList(),
            correct,
        });
        state.Phase = Guard.Move(state.Phase, Phases.Revealed);
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<GuessWhoData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Phases.Revealed, "Reveal the fact before moving on.");
        if (state.Round >= state.Data.Order.Count)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Voting);
            state.Round++;
            state.Data.Result = null;
        }
        await store.SaveStateAsync(state, ct);
    }

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    private static string AuthorOf(SessionState<GuessWhoData> state) => state.Data.Order[state.Round - 1];

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<GuessWhoData>(sessionId, ct)).Phase == Phases.Complete;

    // ---- views ----

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<GuessWhoData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var facts = await store.EntriesAsync<Fact>(sessionId, FactKind, 0, ct);
        var voting = state.Round >= 1;
        var author = voting ? AuthorOf(state) : null;
        var guesses = voting ? await store.EntriesAsync<Guess>(sessionId, GuessKind, state.Round, ct) : [];

        return new GuessWhoPayload(
            state.Phase,
            state.Round,
            state.Data.Order.Count,
            author is null ? null : facts.Single(f => f.Player == author).Value.Text,
            viewer is not null && viewer == author,
            // Once play begins, who did not submit is hidden too: it would rule those players out as authors.
            players.ToDictionary(p => p, p => state.Phase == Submitting && facts.Any(f => f.Player == p)),
            viewer is null ? null : facts.FirstOrDefault(f => f.Player == viewer)?.Value.Text,
            // A count, not a per-player list: a list would leave the author as the one name that never appears.
            guesses.Count,
            players.Count - 1,
            viewer is null ? null : guesses.FirstOrDefault(g => g.Player == viewer)?.Value.Player,
            state.Phase is Phases.Revealed or Phases.Complete ? state.Data.Result : null,
            players.Select(p => new GuessWhoScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new GuessWhoPreview("Everyone writes a fact about themselves; the group guesses who wrote which."));
}
