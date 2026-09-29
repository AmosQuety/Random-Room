using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Rounds;

/// <summary>
/// Runs any prompt-and-answer game: lobby, then rounds of collecting private answers, revealing them together,
/// and moving on. The rules object supplies what is game-specific (prompt shape, answer validation, scoring).
/// Actions: answer (any player), reveal and next (host), tick (any player; honoured only when the server's
/// clock says the deadline has passed).
/// </summary>
public sealed class RoundGameEngine<TPrompt>(IRoundRules<TPrompt> rules, GameStore store, IRandomChoiceSource random, TimeProvider clock) : IGameEngine
{
    private const string AnswerKind = "answer";
    private const int MaxCustomPrompts = 100;
    private const int DefaultRounds = 10;
    private const int MinTimeLimit = 5;
    private const int MaxTimeLimit = 300;

    public string GameType => rules.GameType;
    public int MinPlayers => rules.MinPlayers;
    public int MaxPlayers => rules.MaxPlayers;

    // ---- setup ----

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var prompts = new List<TPrompt>();
        if (SetupJson.Flag(setup, "useBuiltIn", fallback: false) && rules.BuiltInBank is not null)
            prompts.AddRange(ContentBank.Load<TPrompt>(rules.BuiltInBank));
        prompts.AddRange(SetupJson.OptionalArray(setup, "prompts", MaxCustomPrompts, "The prompt list").Select(rules.ParsePrompt));

        if (prompts.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Add at least one prompt, or use the built-in set.");

        var rounds = SetupJson.OptionalInt(setup, "rounds", 1, prompts.Count, "The number of rounds") ?? Math.Min(DefaultRounds, prompts.Count);
        var timeLimit = SetupJson.OptionalInt(setup, "timeLimitSeconds", MinTimeLimit, MaxTimeLimit, "The time limit");

        store.AddSetup(roomId, new RoundSetup<TPrompt>(prompts, rounds, timeLimit));
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        // The order is drawn when the session starts: at creation the room's setup is not saved yet.
        store.AddState(sessionId, Phases.Lobby, new RoundData());
        return Task.CompletedTask;
    }

    public Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        InSessionAsync(roomId, sessionId, async (setup, state, _) =>
        {
            state.Data.Order = random.Shuffle(Enumerable.Range(0, setup.Prompts.Count)).Take(setup.Rounds).ToList();
            state.Data.Scores = [];
            BeginRound(state, setup, round: 1);
            await store.SaveStateAsync(state, ct);
        }, ct);

    // ---- actions ----

    public Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct) =>
        InSessionAsync(roomId, sessionId, async (setup, state, players) =>
        {
            switch (action)
            {
                case "answer": await AnswerAsync(sessionId, actor, payload, setup, state, players, ct); break;
                case "reveal": await RevealByHostAsync(roomId, actor, setup, state, players, ct); break;
                case "next": await NextByHostAsync(roomId, actor, setup, state, ct); break;
                case "tick": await TickAsync(sessionId, setup, state, players, ct); break;
                default: throw new RoomRuleException(RuleViolation.InvalidInput, $"This game has no '{action}' action.");
            }
        }, ct);

    private async Task AnswerAsync(Guid sessionId, string actor, JsonElement? payload, RoundSetup<TPrompt> setup, SessionState<RoundData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Phases.Collecting, "Answers are closed for this round.");
        if (ServerTimer.HasPassed(clock, state.DeadlineAt))
            throw new RoomRuleException(RuleViolation.Conflict, "Time is up for this round.");
        if (payload is null)
            throw new RoomRuleException(RuleViolation.InvalidInput, "An answer needs a value.");

        var value = rules.ValidateAnswer(CurrentPrompt(setup, state), actor, players, payload.Value);
        await store.AddEntryAsync(sessionId, state.Round, AnswerKind, actor, value, "You have already answered this round.", ct);

        if (await store.CountEntriesAsync(sessionId, AnswerKind, state.Round, ct) >= players.Count)
            await RevealAsync(sessionId, setup, state, players, ct);
    }

    private async Task RevealByHostAsync(Guid roomId, string actor, RoundSetup<TPrompt> setup, SessionState<RoundData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        await RequireHostAsync(roomId, actor, ct);
        PhaseGuard.Require(state.Phase, Phases.Collecting, "This round has already been revealed.");
        await RevealAsync(state.Row.SessionId, setup, state, players, ct);
    }

    private async Task TickAsync(Guid sessionId, RoundSetup<TPrompt> setup, SessionState<RoundData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        // A tick that arrives early, late, or in the wrong phase changes nothing: only the server's clock decides.
        if (state.Phase != Phases.Collecting || !ServerTimer.HasPassed(clock, state.DeadlineAt)) return;
        await RevealAsync(sessionId, setup, state, players, ct);
    }

    private async Task NextByHostAsync(Guid roomId, string actor, RoundSetup<TPrompt> setup, SessionState<RoundData> state, CancellationToken ct)
    {
        await RequireHostAsync(roomId, actor, ct);
        PhaseGuard.Require(state.Phase, Phases.Revealed, "Reveal the round before moving on.");

        if (state.Round >= state.Data.Order.Count)
        {
            state.Phase = PhaseGuard.Standard.Move(state.Phase, Phases.Complete);
            state.DeadlineAt = null;
        }
        else
        {
            BeginRound(state, setup, state.Round + 1);
        }
        await store.SaveStateAsync(state, ct);
    }

    private async Task RevealAsync(Guid sessionId, RoundSetup<TPrompt> setup, SessionState<RoundData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        var stored = await store.EntriesAsync<JsonElement>(sessionId, AnswerKind, state.Round, ct);
        var answers = stored.Select(e => new RoundAnswer(e.Player, e.Value)).ToList();
        var outcome = rules.Resolve(CurrentPrompt(setup, state), answers, players);

        foreach (var (player, points) in outcome.Points)
            state.Data.Scores[player] = state.Data.Scores.GetValueOrDefault(player) + points;
        state.Data.Result = GameStore.ToElement(outcome.Summary);
        state.Phase = PhaseGuard.Standard.Move(state.Phase, Phases.Revealed);
        state.DeadlineAt = null;
        await store.SaveStateAsync(state, ct);
    }

    private void BeginRound(SessionState<RoundData> state, RoundSetup<TPrompt> setup, int round)
    {
        state.Phase = PhaseGuard.Standard.Move(state.Phase, Phases.Collecting);
        state.Round = round;
        state.Data.Result = null;
        state.DeadlineAt = ServerTimer.DeadlineIn(clock, setup.TimeLimitSeconds);
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<RoundData>(sessionId, ct)).Phase == Phases.Complete;

    // ---- views ----

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<RoundSetup<TPrompt>>(roomId, ct);
        var state = await store.LoadStateAsync<RoundData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);

        var inRound = state.Round >= 1;
        var entries = inRound ? await store.EntriesAsync<JsonElement>(sessionId, AnswerKind, state.Round, ct) : [];
        var revealed = state.Phase is Phases.Revealed or Phases.Complete;

        return new RoundPayload(
            state.Phase,
            state.Round,
            state.Data.Order.Count == 0 ? setup.Rounds : state.Data.Order.Count,
            inRound ? rules.ViewPromptFor(CurrentPrompt(setup, state), players) : null,
            players.ToDictionary(p => p, p => entries.Any(e => e.Player == p)),
            viewer is null ? null : entries.FirstOrDefault(e => e.Player == viewer)?.Value,
            revealed ? state.Data.Result : null,
            players.Select(p => new RoundScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList(),
            ServerTimer.View(clock, state.DeadlineAt),
            setup.TimeLimitSeconds);
    }

    public async Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<RoundSetup<TPrompt>>(roomId, ct);
        return new RoundPreview(setup.Prompts.Count, setup.Rounds, setup.TimeLimitSeconds);
    }

    // ---- helpers ----

    private static TPrompt CurrentPrompt(RoundSetup<TPrompt> setup, SessionState<RoundData> state) =>
        setup.Prompts[state.Data.Order[state.Round - 1]];

    private async Task RequireHostAsync(Guid roomId, string actor, CancellationToken ct)
    {
        if (actor != await store.HostOfAsync(roomId, ct))
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    /// <summary>Runs one action on one session under a row lock, so answers and reveals cannot interleave.</summary>
    private Task InSessionAsync(Guid roomId, Guid sessionId, Func<RoundSetup<TPrompt>, SessionState<RoundData>, IReadOnlyList<string>, Task> work, CancellationToken ct) =>
        store.InTransactionAsync(async () =>
        {
            await store.LockSessionAsync(sessionId, ct);
            var setup = await store.GetSetupAsync<RoundSetup<TPrompt>>(roomId, ct);
            var state = await store.LoadStateAsync<RoundData>(sessionId, ct);
            var players = await store.PlayerNamesAsync(roomId, ct);
            await work(setup, state, players);
        }, ct);
}
