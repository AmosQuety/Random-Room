using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.SpinWheel;

/// <summary>The host's setup: the segments they typed, whether built-in ones may fill the wheel, and how many spins.</summary>
public sealed record WheelSetup(List<string> Segments, bool UseBuiltIn, int Spins);

public sealed class WheelData
{
    /// <summary>The wheel for this session, fixed when it starts so every player sees the same one.</summary>
    public List<string> Wheel { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];

    public WheelSpin? Last { get; set; }

    public bool Awarded { get; set; }
}

/// <summary>A landed spin. The server chose Index; clients only animate towards it.</summary>
public sealed record WheelSpin(string Player, int Index, string Label);

public sealed record WheelScore(string Player, int Score);

public sealed record WheelPreview(string Rule);

/// <summary>Nothing here is secret: the wheel and every result are public the moment they exist.</summary>
public sealed record WheelPayload(
    string Phase,
    int Round,
    int TotalRounds,
    IReadOnlyList<string> Segments,
    string? Spinner,
    WheelSpin? Last,
    bool Awarded,
    IReadOnlyList<WheelScore> Scoreboard);

/// <summary>
/// Players take turns spinning a wheel. The server picks the segment (a spin cannot be steered or replayed);
/// the client only animates to it. The host may award the spinner one point for completing the challenge.
/// Actions: spin (the player whose turn it is), award / next (host).
/// </summary>
public sealed class SpinWheelEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "spin-wheel";

    public const string Ready = "ready";
    public const string Spun = "spun";

    public const int MinSegments = 2;
    public const int MaxSegments = 12;
    public const int FillTo = 8;
    public const int MaxSpins = 24;
    public const int MaxSegmentLength = 80;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, Ready),
        (Ready, Spun),
        (Spun, Ready),
        (Spun, Phases.Complete));

    public string GameType => Key;

    public async Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var segments = SetupJson.OptionalArray(setup, "segments", MaxSegments, "Wheel segments")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Segment {i + 1} must be text."))
            .ToList();
        foreach (var (segment, i) in segments.Select((s, i) => (s, i)))
        {
            if (segment.Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Segment {i + 1} cannot be empty.");
            if (segment.Length > MaxSegmentLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Segment {i + 1} is too long (at most {MaxSegmentLength} characters).");
        }
        if (segments.Select(AnswerNormalizer.Normalize).Distinct().Count() != segments.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Each wheel segment must be different.");

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", segments.Count == 0);
        if (!useBuiltIn && segments.Count < MinSegments)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Add at least {MinSegments} segments, or use the built-in challenges.");

        var spins = SetupJson.OptionalInt(setup, "spins", 1, MaxSpins, "Number of spins") ?? 6;
        store.AddSetup(roomId, new WheelSetup(segments, useBuiltIn, spins));
        await Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new WheelData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<WheelSetup>(roomId, ct);
        await store.InSessionAsync<WheelData>(sessionId, async state =>
        {
            state.Data.Wheel = BuildWheel(setup).ToList();
            state.Data.Scores = [];
            state.Data.Last = null;
            state.Data.Awarded = false;
            state.Phase = Guard.Move(state.Phase, Ready);
            state.Round = 1;
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    private IEnumerable<string> BuildWheel(WheelSetup setup)
    {
        var wheel = setup.Segments.ToList();
        if (setup.UseBuiltIn && wheel.Count < FillTo)
        {
            var spare = ContentBank.Load<string>("spin-wheel")
                .Where(b => !wheel.Any(w => AnswerNormalizer.Normalize(w) == AnswerNormalizer.Normalize(b)));
            wheel.AddRange(random.Shuffle(spare).Take(FillTo - wheel.Count));
        }
        return wheel;
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("spin" or "award" or "next"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Spin the Wheel has no '{action}' action.");

        var players = await store.PlayerNamesAsync(roomId, ct);
        var host = await store.HostOfAsync(roomId, ct);
        var setup = await store.GetSetupAsync<WheelSetup>(roomId, ct);
        await store.InSessionAsync<WheelData>(sessionId, async state =>
        {
            switch (action)
            {
                case "spin": await SpinAsync(actor, state, players, ct); break;
                case "award": RequireHost(actor, host); await AwardAsync(state, players, ct); break;
                case "next": RequireHost(actor, host); await NextAsync(state, setup, ct); break;
            }
        }, ct);
    }

    private async Task SpinAsync(string actor, SessionState<WheelData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Ready, "The wheel has already been spun this turn.");
        var spinner = SpinnerFor(state.Round, players);
        if (actor != spinner)
            throw new RoomRuleException(RuleViolation.Forbidden, $"It is {spinner}'s turn to spin.");

        var index = random.PickIndex(state.Data.Wheel.Count);
        state.Data.Last = new WheelSpin(actor, index, state.Data.Wheel[index]);
        state.Data.Awarded = false;
        state.Phase = Guard.Move(state.Phase, Spun);
        await store.SaveStateAsync(state, ct);
    }

    private async Task AwardAsync(SessionState<WheelData> state, IReadOnlyList<string> players, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Spun, "Nothing has been spun to award.");
        if (state.Data.Awarded)
            throw new RoomRuleException(RuleViolation.Conflict, "This spin has already been awarded.");

        var spinner = SpinnerFor(state.Round, players);
        state.Data.Scores[spinner] = state.Data.Scores.GetValueOrDefault(spinner) + 1;
        state.Data.Awarded = true;
        await store.SaveStateAsync(state, ct);
    }

    private async Task NextAsync(SessionState<WheelData> state, WheelSetup setup, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Spun, "Spin the wheel before moving on.");
        if (state.Round >= setup.Spins)
        {
            state.Phase = Guard.Move(state.Phase, Phases.Complete);
        }
        else
        {
            state.Phase = Guard.Move(state.Phase, Ready);
            state.Round++;
            state.Data.Last = null;
            state.Data.Awarded = false;
        }
        await store.SaveStateAsync(state, ct);
    }

    private static string SpinnerFor(int round, IReadOnlyList<string> players) => players[(round - 1) % players.Count];

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<WheelData>(sessionId, ct)).Phase == Phases.Complete;

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<WheelData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var setup = await store.GetSetupAsync<WheelSetup>(roomId, ct);
        var playing = state.Round >= 1 && state.Phase != Phases.Complete;
        return new WheelPayload(
            state.Phase,
            state.Round,
            setup.Spins,
            state.Data.Wheel,
            playing ? SpinnerFor(state.Round, players) : null,
            state.Data.Last,
            state.Data.Awarded,
            players.Select(p => new WheelScore(p, state.Data.Scores.GetValueOrDefault(p))).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new WheelPreview("Take turns spinning the wheel and do what it says."));
}
