using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.SketchGuess;

public sealed record SketchSetup(List<string> Words, bool UseBuiltIn, int Rounds, int? TimeLimitSeconds);

/// <summary>
/// One line on the canvas. Points are flattened x,y pairs on a 0..1000 grid, so the drawing looks the same on any
/// screen size. Color indexes the client's palette and Size is the pen width on that grid.
/// </summary>
public sealed record Stroke(int Color, int Size, List<int> Points);

public sealed class SketchData
{
    public List<string> Deck { get; set; } = [];

    /// <summary>The current round's drawing. Cleared when the next round begins and when the game ends; never kept longer.</summary>
    public List<Stroke> Strokes { get; set; } = [];

    public DateTimeOffset? LastCanvasActionAt { get; set; }

    public Dictionary<string, int> Scores { get; set; } = [];

    public JsonElement? Result { get; set; }
}

public sealed record SketchScore(string Player, int Score);

public sealed record SketchPreview(string Rule);

public sealed record SketchGuessView(string Player, string Text);

/// <summary>
/// What a viewer sees. The word is filled only for the drawer while the round is on. The drawing is public: it is
/// exactly what the guessers are meant to look at.
/// </summary>
public sealed record SketchPayload(
    string Phase,
    int Round,
    int TotalRounds,
    string? Drawer,
    string? Word,
    IReadOnlyList<Stroke> Strokes,
    IReadOnlyList<SketchGuessView> Guesses,
    int MyGuessesLeft,
    JsonElement? Result,
    TimerView Timer,
    int? TimeLimitSeconds,
    IReadOnlyList<SketchScore> Scoreboard);

/// <summary>
/// One player draws a secret word on a shared canvas while everyone else types guesses. Only the drawer is ever sent
/// the word, and the server checks every guess (ignoring case, accents and punctuation). The first correct guess ends
/// the round: 2 points for the guesser, 1 for the drawer.
/// The drawing is bounded on the server (strokes, points per stroke, points in total, batch size, and a minimum gap
/// between canvas actions), and is kept only for the round it belongs to.
/// Actions: strokes / undo / clear (drawer), guess, skip (drawer), reveal / next (host), tick (anyone, honoured only after the deadline).
/// </summary>
public sealed class SketchGuessEngine(GameStore store, IRandomChoiceSource random, TimeProvider clock) : IGameEngine
{
    public const string Key = "sketch-guess";

    public const string Drawing = "drawing";

    public const int MaxCustomWords = 60;
    public const int MaxWordLength = 30;
    public const int MaxRounds = 30;
    public const int MaxGuessLength = 40;
    public const int MaxGuessesPerRound = 25;

    public const int GridSize = 1000;
    public const int PaletteSize = 8;
    public const int MinPenSize = 1;
    public const int MaxPenSize = 24;
    public const int MaxStrokeBatch = 20;
    public const int MaxStrokes = 400;
    public const int MaxPointsPerStroke = 200;
    public const int MaxTotalPoints = 6000;

    /// <summary>The shortest gap between two canvas actions from the drawer. Clients batch to stay well inside it.</summary>
    public static readonly TimeSpan MinCanvasGap = TimeSpan.FromMilliseconds(100);

    private const string GuessKind = "guess";

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Drawing),
        (Drawing, Phases.Revealed),
        (Phases.Revealed, Drawing),
        (Phases.Revealed, Phases.Complete));

    public string GameType => Key;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var words = SetupJson.OptionalArray(setup, "words", MaxCustomWords, "Words")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} must be text."))
            .ToList();
        foreach (var (word, i) in words.Select((w, i) => (w, i)))
        {
            if (AnswerNormalizer.Normalize(word).Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} needs letters or numbers.");
            if (word.Length > MaxWordLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} is too long (at most {MaxWordLength} characters).");
        }
        if (words.Select(AnswerNormalizer.Normalize).Distinct().Count() != words.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Each word must be different.");

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", words.Count == 0);
        if (!useBuiltIn && words.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Add at least one word, or use the built-in ones.");

        var rounds = SetupJson.OptionalInt(setup, "rounds", 1, MaxRounds, "Number of rounds") ?? 6;
        var limit = SetupJson.OptionalInt(setup, "timeLimitSeconds", 30, 300, "The time limit") ?? 90;
        store.AddSetup(roomId, new SketchSetup(words, useBuiltIn, rounds, limit));
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new SketchData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<SketchSetup>(roomId, ct);
        await store.InSessionAsync<SketchData>(sessionId, async state =>
        {
            var pool = setup.Words.Concat(setup.UseBuiltIn ? ContentBank.Load<string>("sketch-guess") : []).ToList();
            state.Data = new SketchData { Deck = random.Shuffle(pool).Take(setup.Rounds).ToList() };
            state.Phase = Guard.Move(state.Phase, Drawing);
            state.Round = 1;
            state.DeadlineAt = ServerTimer.DeadlineIn(clock, setup.TimeLimitSeconds);
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("strokes" or "undo" or "clear" or "guess" or "skip" or "reveal" or "next" or "tick"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Sketch Guess has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        var setup = await store.GetSetupAsync<SketchSetup>(roomId, ct);
        await store.InSessionAsync<SketchData>(sessionId, async state =>
        {
            switch (action)
            {
                case "strokes": await AddStrokesAsync(actor, payload, state, players, ct); break;
                case "undo": await UndoAsync(actor, state, players, ct); break;
                case "clear": await ClearAsync(actor, state, players, ct); break;
                case "guess": await GuessAsync(sessionId, actor, payload, state, players, ct); break;
                case "skip": RequireDrawer(actor, state, players); await EndRoundAsync(state, "skipped", null, ct); break;
                case "reveal": RequireHost(actor, host); await EndRoundAsync(state, "ended", null, ct); break;
                case "next": RequireHost(actor, host); await NextAsync(state, setup, ct); break;
                case "tick": await TickAsync(state, ct); break;
            }
        }, ct);
    }

    // ---- canvas ----

    private async Task AddStrokesAsync(string actor, JsonElement? payload, SessionState<SketchData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        RequireCanvas(actor, state, players);
        var incoming = ParseStrokes(payload);
        var total = state.Data.Strokes.Concat(incoming).ToList();
        if (total.Count > MaxStrokes)
            throw new RoomRuleException(RuleViolation.Conflict, "The canvas is full. Clear it or undo something first.");
        if (total.Sum(s => s.Points.Count / 2) > MaxTotalPoints)
            throw new RoomRuleException(RuleViolation.Conflict, "The drawing is too detailed. Clear it or undo something first.");

        state.Data.Strokes = total;
        await store.SaveStateAsync(state, ct);
    }

    private async Task UndoAsync(string actor, SessionState<SketchData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        RequireCanvas(actor, state, players);
        if (state.Data.Strokes.Count == 0)
            throw new RoomRuleException(RuleViolation.Conflict, "There is nothing to undo.");

        state.Data.Strokes = state.Data.Strokes[..^1];
        await store.SaveStateAsync(state, ct);
    }

    private async Task ClearAsync(string actor, SessionState<SketchData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        RequireCanvas(actor, state, players);
        state.Data.Strokes = [];
        await store.SaveStateAsync(state, ct);
    }

    /// <summary>Only the drawer, only while drawing, and no faster than the rate limit allows.</summary>
    private void RequireCanvas(string actor, SessionState<SketchData> state, IReadOnlyList<string> players)
    {
        PhaseGuard.Require(state.Phase, Drawing, "The round is over.");
        RequireDrawer(actor, state, players);

        var now = clock.GetUtcNow();
        if (state.Data.LastCanvasActionAt is { } last && now - last < MinCanvasGap)
            throw new RoomRuleException(RuleViolation.Conflict, "You are drawing too fast. Give it a moment.");
        state.Data.LastCanvasActionAt = now;
    }

    private static List<Stroke> ParseStrokes(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Send the strokes you drew.");
        var raw = SetupJson.OptionalArray(body, "strokes", MaxStrokeBatch, "The strokes");
        if (raw.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Send at least one stroke.");
        return raw.Select(ParseStroke).ToList();
    }

    private static Stroke ParseStroke(JsonElement raw)
    {
        var color = SetupJson.OptionalInt(raw, "color", 0, PaletteSize - 1, "The color") ?? throw new RoomRuleException(RuleViolation.InvalidInput, "A stroke needs a color.");
        var size = SetupJson.OptionalInt(raw, "size", MinPenSize, MaxPenSize, "The pen size") ?? throw new RoomRuleException(RuleViolation.InvalidInput, "A stroke needs a pen size.");
        var values = SetupJson.OptionalArray(raw, "points", MaxPointsPerStroke * 2, "The points");
        if (values.Count == 0 || values.Count % 2 != 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "A stroke needs pairs of x and y coordinates.");

        var points = new List<int>(values.Count);
        foreach (var value in values)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var n) || n < 0 || n > GridSize)
                throw new RoomRuleException(RuleViolation.InvalidInput, $"Coordinates must be whole numbers from 0 to {GridSize}.");
            points.Add(n);
        }
        return new Stroke(color, size, points);
    }

    // ---- guessing and rounds ----

    private async Task GuessAsync(Guid sessionId, string actor, JsonElement? payload, SessionState<SketchData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Drawing, "The round is over.");
        if (actor == Drawer(state, players))
            throw new RoomRuleException(RuleViolation.Forbidden, "You are drawing, so you do not guess.");

        var text = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredText(body, "text", MaxGuessLength) : throw new RoomRuleException(RuleViolation.InvalidInput, "Type a guess.");
        var mine = (await store.EntriesAsync<string>(sessionId, GuessKind, state.Round, ct)).Count(g => g.Player == actor);
        if (mine >= MaxGuessesPerRound)
            throw new RoomRuleException(RuleViolation.Conflict, "You have used all your guesses this round.");

        await store.AddEntryAsync(sessionId, state.Round, GuessKind, actor, text, "That guess was already sent.", ct, seq: mine);

        if (AnswerNormalizer.Normalize(text) == AnswerNormalizer.Normalize(state.Data.Deck[state.Round - 1]))
        {
            Award(state, actor, 2);
            Award(state, Drawer(state, players), 1);
            await EndRoundAsync(state, "guessed", actor, ct);
        }
    }

    private async Task TickAsync(SessionState<SketchData> state, CancellationToken ct)
    {
        // Early, late or out-of-phase ticks change nothing: only the server's clock decides.
        if (state.Phase != Drawing || !ServerTimer.HasPassed(clock, state.DeadlineAt)) return;
        await EndRoundAsync(state, "time", null, ct);
    }

    private async Task EndRoundAsync(SessionState<SketchData> state, string outcome, string? guesser, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Drawing, "The round is already over.");
        state.Data.Result = GameStore.ToElement(new { outcome, guesser, word = state.Data.Deck[state.Round - 1] });
        state.Phase = Guard.Move(state.Phase, Phases.Revealed);
        state.DeadlineAt = null;
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<SketchData> state, SketchSetup setup, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Phases.Revealed, "Finish the round before moving on.");
        // The drawing belongs to its round: it is dropped here, and never carried into the next one.
        state.Data.Strokes = [];
        state.Data.LastCanvasActionAt = null;
        if (state.Round >= state.Data.Deck.Count)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Drawing);
            state.Round++;
            state.Data.Result = null;
            state.DeadlineAt = ServerTimer.DeadlineIn(clock, setup.TimeLimitSeconds);
        }
        await store.SaveStateAsync(state, ct);
    }

    private static void Award(SessionState<SketchData> state, string player, int points) =>
        state.Data.Scores[player] = state.Data.Scores.GetValueOrDefault(player) + points;

    private static string Drawer(SessionState<SketchData> state, IReadOnlyList<string> players) => players[(state.Round - 1) % players.Count];

    private static void RequireDrawer(string actor, SessionState<SketchData> state, IReadOnlyList<string> players)
    {
        if (actor != Drawer(state, players))
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the drawer can do that.");
    }

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<SketchData>(sessionId, ct)).Phase == Phases.Complete;

    // ---- views ----

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<SketchSetup>(roomId, ct);
        var state = await store.LoadStateAsync<SketchData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var inRound = state.Round >= 1 && state.Phase != Phases.Complete;

        var drawer = inRound ? Drawer(state, players) : null;
        var guesses = inRound ? await store.EntriesAsync<string>(sessionId, GuessKind, state.Round, ct) : [];
        var word = inRound && state.Phase == Drawing && viewer is not null && viewer == drawer ? state.Data.Deck[state.Round - 1] : null;

        return new SketchPayload(
            state.Phase,
            state.Round,
            state.Data.Deck.Count == 0 ? setup.Rounds : state.Data.Deck.Count,
            drawer,
            word,
            state.Data.Strokes,
            guesses.Select(g => new SketchGuessView(g.Player, g.Value)).ToList(),
            viewer is null ? 0 : Math.Max(0, MaxGuessesPerRound - guesses.Count(g => g.Player == viewer)),
            state.Phase is Phases.Revealed or Phases.Complete ? state.Data.Result : null,
            ServerTimer.View(clock, state.DeadlineAt),
            setup.TimeLimitSeconds,
            players.Select(p => new SketchScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new SketchPreview("One player draws a secret word while everyone else guesses."));
}
