using System.Text.Json;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.WordSpies;

public static class Team
{
    public const string Red = "red";
    public const string Blue = "blue";

    public static string Other(string team) => team == Red ? Blue : Red;
}

/// <summary>What a board card really is. Kept as plain strings so it serialises into the stored state and the payload as-is.</summary>
public static class Owner
{
    public const string Red = "red";
    public const string Blue = "blue";
    public const string Neutral = "neutral";
    public const string Assassin = "assassin";
}

public sealed record WordSpiesSetup(List<string> Words, bool UseBuiltIn);

public sealed record SpyClue(string Word, int Count, string By);

public sealed class WordSpiesData
{
    public List<string> Words { get; set; } = [];

    /// <summary>The secret key: who each of the 25 cards belongs to. Only ever sent to the two spymasters, or to everyone once the game is over.</summary>
    public List<string> Key { get; set; } = [];

    public List<int> Revealed { get; set; } = [];

    public Dictionary<string, string> Teams { get; set; } = [];

    public Dictionary<string, string> Spymasters { get; set; } = [];

    public string Turn { get; set; } = Team.Red;

    public SpyClue? Clue { get; set; }

    public int GuessesLeft { get; set; }

    public int GuessesMade { get; set; }

    public string? Winner { get; set; }

    public string? EndReason { get; set; }

    public List<SpyClue> ClueLog { get; set; } = [];
}

/// <summary>A board card as any viewer may see it: the owner appears only once the card has been revealed.</summary>
public sealed record SpyCard(string Word, string? Owner);

public sealed record SpyScore(string Player, int Score);

public sealed record WordSpiesPreview(string Rule);

public sealed record WordSpiesPayload(
    string Phase,
    IReadOnlyList<SpyCard> Board,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Teams,
    IReadOnlyDictionary<string, string> Spymasters,
    string? MyTeam,
    bool IsSpymaster,
    IReadOnlyList<string>? Key,
    string? Turn,
    SpyClue? Clue,
    int GuessesLeft,
    IReadOnlyDictionary<string, int> Remaining,
    string? Winner,
    string? EndReason,
    IReadOnlyList<SpyClue> ClueLog,
    IReadOnlyList<SpyScore> Scoreboard);

/// <summary>
/// Two teams race to find their own words on a 5x5 grid. Each team has a spymaster who alone sees the key and
/// gives one-word clues; the rest of the team guess. The key, the assassin and every rule are held and enforced on the
/// server: the key is only in a spymaster's own payload (and everyone's once the game has ended).
/// Teams are dealt at random when the game starts. The team going first has 9 cards, the other 8, with 7 neutral and 1 assassin.
/// Scoring: everyone on the winning team scores 1.
/// Actions: clue (spymaster of the team in play), guess / pass (that team's other members), end (host).
/// </summary>
public sealed class WordSpiesEngine(GameStore store, IRandomChoiceSource random) : IGameEngine
{
    public const string Key = "word-spies";

    public const string GivingClue = "clue";
    public const string Guessing = "guessing";

    public const int Cells = 25;
    public const int FirstTeamCards = 9;
    public const int SecondTeamCards = 8;
    public const int NeutralCards = 7;
    public const int MaxWordLength = 24;
    public const int MaxCustomWords = 100;
    public const int MaxClueLength = 24;
    public const int MaxClueCount = 9;

    private static readonly PhaseGuard Guard = new(
        (Phases.Lobby, GivingClue),
        (GivingClue, Guessing),
        (Guessing, GivingClue),
        (Guessing, Phases.Complete),
        (GivingClue, Phases.Complete));

    public string GameType => Key;
    public int MinPlayers => 4;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var words = SetupJson.OptionalArray(setup, "words", MaxCustomWords, "Words")
            .Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} must be text."))
            .ToList();
        foreach (var (word, i) in words.Select((w, i) => (w, i)))
        {
            if (word.Length == 0) throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} cannot be empty.");
            if (word.Length > MaxWordLength) throw new RoomRuleException(RuleViolation.InvalidInput, $"Word {i + 1} is too long (at most {MaxWordLength} characters).");
        }
        if (words.Select(AnswerNormalizer.Normalize).Distinct().Count() != words.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Each word must be different.");

        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", words.Count == 0);
        var setupValue = new WordSpiesSetup(words, useBuiltIn);
        if (Pool(setupValue).Count < Cells)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The board needs at least {Cells} different words, or use the built-in ones.");

        store.AddSetup(roomId, setupValue);
        return Task.CompletedTask;
    }

    private static List<string> Pool(WordSpiesSetup setup)
    {
        var pool = setup.Words.ToList();
        if (setup.UseBuiltIn)
            pool.AddRange(ContentBank.Load<string>("word-spies").Where(b => !pool.Any(p => AnswerNormalizer.Normalize(p) == AnswerNormalizer.Normalize(b))));
        return pool;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        store.AddState(sessionId, Phases.Lobby, new WordSpiesData());
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var setup = await store.GetSetupAsync<WordSpiesSetup>(roomId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        await store.InSessionAsync<WordSpiesData>(sessionId, async state =>
        {
            state.Data = Deal(Pool(setup), players);
            state.Phase = Guard.Move(state.Phase, GivingClue);
            state.Round = 1;
            await store.SaveStateAsync(state, ct);
        }, ct);
    }

    private WordSpiesData Deal(List<string> pool, IReadOnlyList<string> players)
    {
        var first = random.PickIndex(2) == 0 ? Team.Red : Team.Blue;
        var second = Team.Other(first);

        var owners = Enumerable.Repeat(first, FirstTeamCards)
            .Concat(Enumerable.Repeat(second, SecondTeamCards))
            .Concat(Enumerable.Repeat(Owner.Neutral, NeutralCards))
            .Append(Owner.Assassin);

        var teams = new Dictionary<string, string>();
        var spymasters = new Dictionary<string, string>();
        foreach (var (player, i) in random.Shuffle(players).Select((p, i) => (p, i)))
        {
            var team = i % 2 == 0 ? Team.Red : Team.Blue;
            teams[player] = team;
            spymasters.TryAdd(team, player);
        }

        return new WordSpiesData
        {
            Words = random.Shuffle(pool).Take(Cells).ToList(),
            Key = random.Shuffle(owners).ToList(),
            Teams = teams,
            Spymasters = spymasters,
            Turn = first,
        };
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("clue" or "guess" or "pass" or "end"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Word Spies has no '{action}' action.");

        var host = await store.HostOfAsync(roomId, ct);
        await store.InSessionAsync<WordSpiesData>(sessionId, async state =>
        {
            switch (action)
            {
                case "clue": await ClueAsync(actor, payload, state, ct); break;
                case "guess": await GuessAsync(actor, payload, state, ct); break;
                case "pass": await PassAsync(actor, state, ct); break;
                case "end": RequireHost(actor, host); await EndAsync(state, ct); break;
            }
        }, ct);
    }

    private async Task ClueAsync(string actor, JsonElement? payload, SessionState<WordSpiesData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, GivingClue, "A clue has already been given this turn.");
        if (state.Data.Spymasters[state.Data.Turn] != actor)
            throw new RoomRuleException(RuleViolation.Forbidden, $"Only the {state.Data.Turn} team's spymaster can give the clue now.");

        var body = payload is { ValueKind: JsonValueKind.Object } b ? b : throw new RoomRuleException(RuleViolation.InvalidInput, "Give a one-word clue and a number.");
        var word = AnswerJson.RequiredText(body, "word", MaxClueLength);
        var count = AnswerJson.RequiredInt(body, "count");
        ValidateClue(word, count, state.Data);

        state.Data.Clue = new SpyClue(word, count, actor);
        state.Data.ClueLog.Add(state.Data.Clue);
        state.Data.GuessesLeft = count + 1;
        state.Data.GuessesMade = 0;
        state.Phase = Guard.Move(state.Phase, Guessing);
        await store.SaveStateAsync(state, ct);
    }

    private static void ValidateClue(string word, int count, WordSpiesData data)
    {
        if (word.Any(c => !char.IsLetter(c)))
            throw new RoomRuleException(RuleViolation.InvalidInput, "A clue is a single word made of letters.");
        if (count is < 1 or > MaxClueCount)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The number must be from 1 to {MaxClueCount}.");

        var normalized = AnswerNormalizer.Normalize(word);
        var onBoard = data.Words.Where((w, i) => !data.Revealed.Contains(i)).Any(w => AnswerNormalizer.Normalize(w) == normalized);
        if (onBoard)
            throw new RoomRuleException(RuleViolation.InvalidInput, "A clue cannot be a word that is still on the board.");
    }

    private async Task GuessAsync(string actor, JsonElement? payload, SessionState<WordSpiesData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Guessing, "Wait for the spymaster's clue.");
        var data = state.Data;
        RequireOperative(actor, data);

        var cell = payload is { ValueKind: JsonValueKind.Object } body ? AnswerJson.RequiredInt(body, "cell") : throw new RoomRuleException(RuleViolation.InvalidInput, "Pick a card.");
        if (cell < 0 || cell >= Cells)
            throw new RoomRuleException(RuleViolation.InvalidInput, "That card is not on the board.");
        if (data.Revealed.Contains(cell))
            throw new RoomRuleException(RuleViolation.Conflict, "That card is already turned over.");

        data.Revealed.Add(cell);
        data.GuessesMade++;
        var owner = data.Key[cell];
        var mine = data.Turn;

        if (owner == Owner.Assassin)
        {
            Finish(state, Team.Other(mine), "assassin");
        }
        else if (RemainingFor(data, Team.Other(mine)) == 0)
        {
            Finish(state, Team.Other(mine), "all-found");
        }
        else if (RemainingFor(data, mine) == 0)
        {
            Finish(state, mine, "all-found");
        }
        else if (owner == mine && --data.GuessesLeft > 0)
        {
            // The team may keep guessing.
        }
        else
        {
            EndTurn(state);
        }
        await store.SaveStateAsync(state, ct);
    }

    private async Task PassAsync(string actor, SessionState<WordSpiesData> state, CancellationToken ct)
    {
        PhaseGuard.Require(state.Phase, Guessing, "There is no turn to pass.");
        RequireOperative(actor, state.Data);
        if (state.Data.GuessesMade == 0)
            throw new RoomRuleException(RuleViolation.Conflict, "Make at least one guess before ending the turn.");

        EndTurn(state);
        await store.SaveStateAsync(state, ct);
    }

    private async Task EndAsync(SessionState<WordSpiesData> state, CancellationToken ct)
    {
        if (state.Phase == Phases.Complete)
            throw new RoomRuleException(RuleViolation.Conflict, "The game is already over.");
        state.Phase = Guard.Move(state.Phase, Phases.Complete);
        state.Data.EndReason = "ended";
        await store.SaveStateAsync(state, ct);
    }

    private void EndTurn(SessionState<WordSpiesData> state)
    {
        state.Data.Turn = Team.Other(state.Data.Turn);
        state.Data.Clue = null;
        state.Data.GuessesLeft = 0;
        state.Data.GuessesMade = 0;
        state.Phase = Guard.Move(state.Phase, GivingClue);
    }

    private void Finish(SessionState<WordSpiesData> state, string winner, string reason)
    {
        state.Data.Winner = winner;
        state.Data.EndReason = reason;
        state.Data.Clue = null;
        state.Phase = Guard.Move(state.Phase, Phases.Complete);
    }

    private static void RequireOperative(string actor, WordSpiesData data)
    {
        if (data.Teams.GetValueOrDefault(actor) != data.Turn)
            throw new RoomRuleException(RuleViolation.Forbidden, $"It is the {data.Turn} team's turn.");
        if (data.Spymasters[data.Turn] == actor)
            throw new RoomRuleException(RuleViolation.Forbidden, "The spymaster gives clues but does not guess.");
    }

    private static int RemainingFor(WordSpiesData data, string team) =>
        data.Key.Where((owner, i) => owner == team && !data.Revealed.Contains(i)).Count();

    private static void RequireHost(string actor, string host)
    {
        if (actor != host)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can do that.");
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct) =>
        (await store.LoadStateAsync<WordSpiesData>(sessionId, ct)).Phase == Phases.Complete;

    public Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer: null, ct);

    public Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        BuildPayloadAsync(roomId, sessionId, viewer, ct);

    private async Task<object> BuildPayloadAsync(Guid roomId, Guid sessionId, string? viewer, CancellationToken ct)
    {
        var state = await store.LoadStateAsync<WordSpiesData>(sessionId, ct);
        var players = await store.PlayerNamesAsync(roomId, ct);
        var data = state.Data;
        var started = data.Key.Count == Cells;
        var over = state.Phase == Phases.Complete;

        var myTeam = viewer is null ? null : data.Teams.GetValueOrDefault(viewer);
        var isSpymaster = viewer is not null && data.Spymasters.ContainsValue(viewer);
        // The key leaves the server only for a spymaster, or for everyone once nothing is left to protect.
        var showKey = started && (isSpymaster || over);

        return new WordSpiesPayload(
            state.Phase,
            data.Words.Select((w, i) => new SpyCard(w, data.Revealed.Contains(i) || over ? data.Key[i] : null)).ToList(),
            new[] { Team.Red, Team.Blue }.ToDictionary(t => t, t => (IReadOnlyList<string>)players.Where(p => data.Teams.GetValueOrDefault(p) == t).ToList()),
            data.Spymasters,
            myTeam,
            isSpymaster,
            showKey ? data.Key : null,
            started && !over ? data.Turn : null,
            data.Clue,
            data.GuessesLeft,
            started ? new Dictionary<string, int> { [Team.Red] = RemainingFor(data, Team.Red), [Team.Blue] = RemainingFor(data, Team.Blue) } : new Dictionary<string, int>(),
            data.Winner,
            data.EndReason,
            data.ClueLog,
            players.Select(p => new SpyScore(p, data.Winner is not null && data.Teams.GetValueOrDefault(p) == data.Winner ? 1 : 0)).ToList());
    }

    public Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        Task.FromResult<object>(new WordSpiesPreview("Two teams, one grid of words, and a spymaster on each side who knows where everything is."));
}
