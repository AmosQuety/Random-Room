using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.ForbiddenWords;

public sealed record TabooCard(string Word, List<string> Forbidden);

public sealed record ForbiddenSetup(List<TabooCard> Cards, bool UseBuiltIn, int Rounds, int? TimeLimitSeconds);

public sealed class ForbiddenData
{
    /// <summary>The cards in play order, chosen when the session starts. Never sent except the current one, and only to its describer and judge.</summary>
    public List<TabooCard> Deck { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    public JsonElement? Result { get; set; }

    /// <summary>Points the host changed (only possible when the host is the judge), so they are visible to everyone.</summary>
    public List<HostScoreNote> HostScoring { get; set; } = [];
}

public sealed record ForbiddenScore(string Player, int Score);

public sealed record ForbiddenPreview(string Rule);

public sealed record GuessView(string Player, string Text);

/// <summary>The card as its describer and judge see it.</summary>
public sealed record TabooCardView(string Word, IReadOnlyList<string> Forbidden);

/// <summary>
/// What a viewer sees. Card is filled only for the round's describer and judge (and nobody else) until the round is
/// over; the public snapshot never has it. Guesses are public: they are said out loud anyway.
/// </summary>
public sealed record ForbiddenPayload(
    string Phase,
    int Round,
    int TotalRounds,
    string? Describer,
    string? Judge,
    string? Role,
    TabooCardView? Card,
    IReadOnlyList<GuessView> Guesses,
    int MyGuessesLeft,
    JsonElement? Result,
    TimerView Timer,
    int? TimeLimitSeconds,
    IReadOnlyList<ForbiddenScore> Scoreboard,
    IReadOnlyList<HostScoreNote> HostScoring);

/// <summary>
/// One player describes a secret word without saying the words on its card; everyone else types guesses. The judge
/// (the next player in turn) also sees the card and is the only one who can flag a slip. Scoring: a correct guess
/// scores 1 for the guesser and 1 for the describer. A flagged slip costs the describer 1 point (never below 0).
/// The card is hidden from guessers; the server matches guesses (ignoring case, accents and punctuation).
/// Actions: guess, skip (describer), flag (judge only), reveal / next (host), tick (anyone, honoured only after the deadline).
/// </summary>
public sealed class ForbiddenWordsEngine(GameStore store, IRandomChoiceSource random, TimeProvider clock) : IGameEngine
{
    public const string Key = "forbidden-words";

    public const string Playing = "playing";

    public const int MaxCustomCards = 40;
    public const int MaxWordLength = 30;
    public const int MaxForbidden = 8;
    public const int MaxGuessLength = 40;
    public const int MaxGuessesPerRound = 25;
    public const int MaxRounds = 40;

    private const string GuessKind = "guess";

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Playing),
        (Playing, Phases.Revealed),
        (Phases.Revealed, Playing),
        (Phases.Revealed, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 3;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var cards = SetupJson.OptionalArray(setup, "cards", MaxCustomCards, "Cards").Select(ParseCard).ToList();
        if (cards.Select(c => AnswerNormalizer.Normalize(c.Word)).Distinct().Count() != cards.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Each card needs a different word.");

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", cards.Count == 0);
        if (!useBuiltIn && cards.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Add at least one card, or use the built-in ones.");

        var rounds = SetupJson.OptionalInt(setup, "rounds", 1, MaxRounds, "Number of rounds") ?? 6;
        var limit = SetupJson.OptionalInt(setup, "timeLimitSeconds", 15, 300, "The time limit");
        store.AddSetup(roomId, new ForbiddenSetup(cards, useBuiltIn, rounds, limit));
        return Task.CompletedTask;
    }

    private static TabooCard ParseCard(JsonElement raw)
    {
        var word = SetupJson.RequiredText(raw, "word", MaxWordLength, "The card's word");
        var forbidden = SetupJson.OptionalArray(raw, "forbidden", MaxForbidden, "The forbidden words")
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, "Forbidden words must be text."))
            .Where(f => f.Length > 0)
            .ToList();
        if (forbidden.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{word}' needs at least one forbidden word.");
        if (forbidden.Any(f => f.Length > MaxWordLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Forbidden words can be at most {MaxWordLength} characters.");
        if (forbidden.Any(f => AnswerNormalizer.Normalize(f) == AnswerNormalizer.Normalize(word)))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{word}' cannot be one of its own forbidden words.");
        return new TabooCard(word, forbidden);
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new ForbiddenData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<ForbiddenSetup>(roomId, ct);
        await store.InSessionAsync<ForbiddenData>(sessionId, async state =>
        {
            var pool = setup.Cards.Concat(setup.UseBuiltIn ? ContentBank.Load<TabooCard>("forbidden-words") : []).ToList();
            state.Data.Deck = random.Shuffle(pool).Take(setup.Rounds).ToList();
            state.Data.Scores = [];
            state.Data.Result = null;
            state.Phase = Guard.Move(state.Phase, Playing);
            state.Round = 1;
            state.DeadlineAt = ServerTimer.DeadlineIn(clock, setup.TimeLimitSeconds);
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("guess" or "skip" or "flag" or "reveal" or "next" or "tick"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Forbidden Words has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        var setup = await store.GetSetupAsync<ForbiddenSetup>(roomId, ct);
        await store.InSessionAsync<ForbiddenData>(sessionId, async state =>
        {
            switch (action)
            {
                case "guess": await GuessAsync(sessionId, actor, payload, state, players, ct); break;
                case "skip": RequireRole(actor, Describer(state, players), "Only the describer can skip the card."); await EndRoundAsync(state, "skipped", null, ct); break;
                case "flag": await FlagAsync(actor, host, state, players, ct); break;
                case "reveal": RequireHost(actor, host); await EndRoundAsync(state, "ended", null, ct); break;
                case "next": RequireHost(actor, host); await NextAsync(state, setup, ct); break;
                case "tick": await TickAsync(state, ct); break;
            }
        }, ct);
    }

    private async Task GuessAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<ForbiddenData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Playing, "The round is over.");
        if (actor == Describer(state, players) || actor == Judge(state, players))
            throw new RoomRuleException(RuleViolation.Forbidden, "You can see the card, so you do not guess this round.");

        var text = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredText(body, "text", MaxGuessLength) : throw new RoomRuleException(RuleViolation.InvalidInput, "Type a guess.");
        var mine = (await store.EntriesAsync<string>(sessionId, GuessKind, state.Round, ct)).Count(g => g.Player == actor);
        if (mine >= MaxGuessesPerRound)
            throw new RoomRuleException(RuleViolation.Conflict, "You have used all your guesses this round.");

        await store.AddEntryAsync(sessionId, state.Round, GuessKind, actor, text, "That guess was already sent.", ct, seq: mine);

        if (AnswerNormalizer.Normalize(text) == AnswerNormalizer.Normalize(CurrentCard(state).Word))
        {
            Award(state, actor, 1);
            Award(state, Describer(state, players), 1);
            await EndRoundAsync(state, "guessed", actor, ct);
        }
    }

    private async Task FlagAsync(string actor, string host, SessionState<ForbiddenData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Playing, "The round is over.");
        var describer = Describer(state, players);
        if (actor == describer)
            throw new RoomRuleException(RuleViolation.Forbidden, "You cannot flag yourself.");
        // Only the judge sees the card, so only the judge can tell whether a forbidden word was said. The host gets no
        // blind flag: it would let a host who is also playing punish a rival without knowing the card.
        if (actor != Judge(state, players))
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the judge can flag a slip, because only the judge can see the card.");

        state.Data.Scores[describer] = Math.Max(0, state.Data.Scores.GetValueOrDefault(describer) - 1);
        if (actor == host)
            state.Data.HostScoring.Add(new HostScoreNote(state.Round, describer, -1, "The host, as judge, flagged a slip"));
        await EndRoundAsync(state, "flagged", null, ct);
    }

    private async Task TickAsync(SessionState<ForbiddenData> state, CancellationToken ct)
    {
        // Early, late or out-of-phase ticks change nothing: only the server's clock decides.
        if (state.Phase != Playing || !ServerTimer.HasPassed(clock, state.DeadlineAt)) return;
        await EndRoundAsync(state, "time", null, ct);
    }

    private async Task EndRoundAsync(SessionState<ForbiddenData> state, string outcome, string? guesser, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Playing, "The round is already over.");
        var card = CurrentCard(state);
        state.Data.Result = GameStore.ToElement(new { outcome, guesser, word = card.Word, forbidden = card.Forbidden });
        state.Phase = Guard.Move(state.Phase, Phases.Revealed);
        state.DeadlineAt = null;
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<ForbiddenData> state, ForbiddenSetup setup, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Phases.Revealed, "Finish the round before moving on.");
        if (state.Round >= state.Data.Deck.Count)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Playing);
            state.Round++;
            state.Data.Result = null;
            state.DeadlineAt = ServerTimer.DeadlineIn(clock, setup.TimeLimitSeconds);
        }
        await store.SaveStateAsync(state, ct);
    }

    private static void Award(SessionState<ForbiddenData> state, string player, int points) =>
        state.Data.Scores[player] = state.Data.Scores.GetValueOrDefault(player) + points;

    private static TabooCard CurrentCard(SessionState<ForbiddenData> state) => state.Data.Deck[state.Round - 1];

    private static string Describer(SessionState<ForbiddenData> state, IReadOnlyList<string> players) => players[(state.Round - 1) % players.Count];

    private static string Judge(SessionState<ForbiddenData> state, IReadOnlyList<string> players) => players[state.Round % players.Count];

    private static void RequireRole(string actor, string expected, string message)
    {
        if (actor != expected)
            throw new RoomRuleException(RuleViolation.Forbidden, message);
    }

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<ForbiddenData>(sessionId, ct)).Phase == Phases.Complete;

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<ForbiddenSetup>(roomId, ct);
        var state = await store.LoadStateAsync<ForbiddenData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var inRound = state.Round >= 1 && state.Phase != Phases.Complete;

        var describer = inRound ? Describer(state, players) : null;
        var judge = inRound ? Judge(state, players) : null;
        var guesses = inRound ? await store.EntriesAsync<string>(sessionId, GuessKind, state.Round, ct) : [];
        var sees = viewer is not null && (viewer == describer || viewer == judge);
        var role = viewer is null || !inRound ? null : viewer == describer ? "describer" : viewer == judge ? "judge" : "guesser";
        var card = inRound && state.Phase == Playing && sees ? CurrentCard(state) : null;

        return new ForbiddenPayload(
            state.Phase,
            state.Round,
            state.Data.Deck.Count == 0 ? setup.Rounds : state.Data.Deck.Count,
            describer,
            judge,
            role,
            card is null ? null : new TabooCardView(card.Word, card.Forbidden),
            guesses.Select(g => new GuessView(g.Player, g.Value)).ToList(),
            viewer is null ? 0 : Math.Max(0, MaxGuessesPerRound - guesses.Count(g => g.Player == viewer)),
            state.Phase is Phases.Revealed or Phases.Complete ? state.Data.Result : null,
            ServerTimer.View(clock, state.DeadlineAt),
            setup.TimeLimitSeconds,
            players.Select(p => new ForbiddenScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList(),
            state.Data.HostScoring);
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new ForbiddenPreview("Describe the secret word without saying the forbidden ones. Everyone else guesses."));
}
