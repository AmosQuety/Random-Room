using System.Text.Json;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Bingo;
using RandomRoom.Api.Games.Buzzer;
using RandomRoom.Api.Games.ForbiddenWords;
using RandomRoom.Api.Games.GuessWho;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Games.SketchGuess;
using RandomRoom.Api.Games.SpinWheel;
using RandomRoom.Api.Games.StoryChain;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Games.TwoTruths;
using RandomRoom.Api.Games.WordSpies;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>A host who referees runs every game but never plays it, and is never counted as a participant.</summary>
public class RefereeHostTests
{
    private const string Referee = "Rita";

    private static readonly string[] Others = ["Lydia", "James", "Jacob", "Ruth", "Sam", "Noah", "Esther", "Paul", "Mary", "John", "Anna", "Peter"];

    /// <summary>One game: how to build it, a valid setup, and every action that is for participants only.</summary>
    public sealed record Game(string Key, Func<EngineDeps, IGameEngine> Factory, object Setup, string[] PlayActions)
    {
        public override string ToString() => Key;
    }

    private static readonly Game[] Games =
    [
        new(RandomPickerEngine.Key, d => new RandomPickerEngine(d.Db, d.Random, d.Clock), new { choices = new[] { "A", "B" } }, ["trigger"]),
        new(TriviaEngine.Key, d => new TriviaEngine(d.Db, d.Clock, d.Random),
            new { questions = new[] { new { text = "2 + 2?", options = new[] { "3", "4" }, correctIndex = 1 } } }, ["answer"]),
        new(TwoTruthsEngine.Key, d => new TwoTruthsEngine(d.Store, d.Random), new { }, ["submit", "vote"]),
        new(GuessWhoEngine.Key, d => new GuessWhoEngine(d.Store, d.Random), new { }, ["submit", "guess"]),
        new(SpinWheelEngine.Key, d => new SpinWheelEngine(d.Store, d.Random), new { segments = new[] { "Sing", "Dance" }, useBuiltIn = false, spins = 2 }, ["spin"]),
        new(BuzzerEngine.Key, d => new BuzzerEngine(d.Store, d.Random), new { prompts = new[] { "Name a fruit" }, useBuiltIn = false, rounds = 1 }, ["buzz"]),
        new(BingoEngine.Key, d => new BingoEngine(d.Store, d.Random), new { useBuiltIn = true }, ["mark", "bingo"]),
        new(WordSpiesEngine.Key, d => new WordSpiesEngine(d.Store, d.Random), new { useBuiltIn = true }, ["clue", "guess", "pass"]),
        new(ForbiddenWordsEngine.Key, d => new ForbiddenWordsEngine(d.Store, d.Random, d.Clock), ForbiddenSetup, ["guess", "skip"]),
        new(SketchGuessEngine.Key, d => new SketchGuessEngine(d.Store, d.Random, d.Clock),
            new { words = new[] { "Sailboat", "Cactus" }, useBuiltIn = false, rounds = 2, timeLimitSeconds = 60 }, ["strokes", "undo", "clear", "guess", "skip"]),
        new("one-word-story", d => new StoryChainEngine(new OneWordRules(), d.Store, d.Random), new { useBuiltIn = true, length = 10 }, ["add"]),
        new("fortunately", d => new StoryChainEngine(new FortunatelyRules(), d.Store, d.Random), new { useBuiltIn = true, length = 4 }, ["add"]),
        new("name-that", d => new RoundGameEngine<IntroPrompt>(new NameThatRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { clue = "A yellow submarine", answers = new[] { "Yellow Submarine" } } }, rounds = 1 }, ["answer"]),
        new("mad-libs", d => new RoundGameEngine<MadLibPrompt>(new MadLibsRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { title = "Story", text = "The {adjective} {animal} went to the {place}." } }, rounds = 1 }, ["answer"]),
        new("would-you-rather", d => new RoundGameEngine<TwoWayPrompt>(new WouldYouRatherRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { a = "Fly", b = "Swim" } }, rounds = 1 }, ["answer"]),
        new("this-or-that", d => new RoundGameEngine<TwoWayPrompt>(new ThisOrThatRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { a = "Tea", b = "Coffee" } }, rounds = 1 }, ["answer"]),
        new("most-likely-to", d => new RoundGameEngine<StatementPrompt>(new MostLikelyToRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { text = "Most likely to nap" } }, rounds = 1 }, ["answer"]),
        new("never-have-i-ever", d => new RoundGameEngine<StatementPrompt>(new NeverHaveIEverRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { text = "Never have I ever built a fort" } }, rounds = 1 }, ["answer"]),
        new("survey-showdown", d => new RoundGameEngine<SurveyPrompt>(new SurveyShowdownRules(), d.Store, d.Random, d.Clock),
            new { prompts = new[] { new { text = "Name a pet", answers = new object[] { new { text = "Dog", points = 40 }, new { text = "Cat", points = 30 } } } }, rounds = 1 }, ["answer"]),
    ];

    private static readonly object ForbiddenSetup = new
    {
        cards = new[] { new { word = "Banana", forbidden = new[] { "Yellow", "Fruit", "Monkey" } }, new { word = "Rain", forbidden = new[] { "Water", "Cloud", "Wet" } } },
        useBuiltIn = false,
        rounds = 2,
        timeLimitSeconds = 60,
    };

    public static TheoryData<Game> AllGames()
    {
        var data = new TheoryData<Game>();
        foreach (var game in Games) data.Add(game);
        return data;
    }

    public static TheoryData<Game, string> AllPlayActions()
    {
        var data = new TheoryData<Game, string>();
        foreach (var game in Games)
            foreach (var action in game.PlayActions) data.Add(game, action);
        return data;
    }

    private static int MinParticipants(Game game) =>
        game.Factory(new EngineDeps(null!, null!, new ManualClock(), new ScriptedRandom())).MinPlayersWithReferee;

    private static string[] Seats(int participants) => [Referee, .. Others.Take(participants)];

    private static GameHarness RefereeRoom(Game game, int participants) =>
        new(game.Factory, game.Key, game.Setup, Seats(participants), new SeededRandom(), hostPlays: false);

    private static GameHarness RefereeRoom(Game game) => RefereeRoom(game, Math.Max(MinParticipants(game), 4));

    [Theory]
    [MemberData(nameof(AllGames))]
    public async Task Referee_is_absent_from_the_game_payload(Game game)
    {
        using var room = RefereeRoom(game);
        await room.StartAsync();

        var asParticipant = JsonSerializer.Serialize((await room.SnapshotAsync(Others[0])).GamePayload);
        var asReferee = JsonSerializer.Serialize((await room.SnapshotAsync(Referee)).GamePayload);

        Assert.DoesNotContain(Referee, asParticipant);
        Assert.DoesNotContain(Referee, asReferee);
    }

    [Theory]
    [MemberData(nameof(AllGames))]
    public async Task Snapshot_marks_the_host_as_not_playing(Game game)
    {
        using var room = RefereeRoom(game);

        var snapshot = await room.SnapshotAsync(Others[0]);

        Assert.False(snapshot.HostPlays);
        Assert.False(snapshot.Players.Single(p => p.Name == Referee).Plays);
        Assert.All(snapshot.Players.Where(p => p.Name != Referee), p => Assert.True(p.Plays));
    }

    [Theory]
    [MemberData(nameof(AllPlayActions))]
    public async Task Referee_cannot_play(Game game, string action)
    {
        using var room = RefereeRoom(game);
        await room.StartAsync();

        var refused = await GameHarness.RejectedAsync(() => room.ActAsync(Referee, action, new { }));

        Assert.Equal(RuleViolation.Forbidden, refused.Violation);
        Assert.Contains("refereeing", refused.Message);
    }

    [Theory]
    [MemberData(nameof(AllGames))]
    public async Task Game_starts_with_exactly_the_fewest_participants(Game game)
    {
        using var room = RefereeRoom(game, MinParticipants(game));

        var started = await room.StartAsync();

        Assert.Equal(SessionStatus.Active, started.Session.Status);
    }

    [Theory]
    [MemberData(nameof(AllGames))]
    public void Room_with_one_participant_too_few_is_refused(Game game)
    {
        var fewer = MinParticipants(game) - 1;

        var refused = Assert.Throws<RoomRuleException>(() => RefereeRoom(game, fewer).Dispose());

        Assert.Equal(RuleViolation.InvalidInput, refused.Violation);
    }

    [Fact]
    public void Seat_cap_counts_the_referee()
    {
        var picker = Games[0];

        var refused = Assert.Throws<RoomRuleException>(() => RefereeRoom(picker, 12).Dispose());

        Assert.Contains("at most 12", refused.Message);
    }

    [Fact]
    public async Task Eleven_participants_and_a_referee_fill_the_room()
    {
        using var room = RefereeRoom(Games[0], 11);

        var snapshot = await room.StartAsync();

        Assert.Equal(12, snapshot.Players.Count);
    }

    [Fact]
    public void Buzzer_needs_two_participants_with_a_referee_but_three_seats_when_the_host_plays()
    {
        var buzzer = Games.Single(g => g.Key == BuzzerEngine.Key);

        Assert.Equal(2, MinParticipants(buzzer));
        Assert.Throws<RoomRuleException>(() =>
            new GameHarness(buzzer.Factory, buzzer.Key, buzzer.Setup, ["Amos", "Lydia"]).Dispose());
    }

    [Fact]
    public async Task Random_picker_completes_when_every_participant_has_gone()
    {
        using var room = RefereeRoom(Games[0], 2);
        await room.StartAsync();

        await room.ActAsync(Others[0], "trigger");
        var last = await room.ActAsync(Others[1], "trigger");

        Assert.Equal(SessionStatus.Completed, last.Session.Status);
    }

    [Fact]
    public async Task Trivia_moves_on_when_every_participant_has_answered()
    {
        var trivia = Games.Single(g => g.Key == TriviaEngine.Key);
        using var room = RefereeRoom(trivia, 2);
        await room.StartAsync();

        await room.ActAsync(Others[0], "answer", new { optionIndex = 1 });
        var last = await room.ActAsync(Others[1], "answer", new { optionIndex = 0 });

        Assert.Equal(SessionStatus.Completed, last.Session.Status);
    }

    [Fact]
    public async Task Referee_runs_the_game_with_control_actions()
    {
        var buzzer = Games.Single(g => g.Key == BuzzerEngine.Key);
        using var room = RefereeRoom(buzzer, 2);
        await room.StartAsync();

        await room.ActAsync(Referee, "open");
        await room.ActAsync(Others[0], "buzz");
        var judged = await room.ActAsync(Referee, "correct");

        var payload = (BuzzerPayload)judged.GamePayload;
        Assert.Equal(Others[0], payload.Winner);
    }

    [Fact]
    public async Task Spin_wheel_referee_gives_the_point_and_never_spins()
    {
        var wheel = Games.Single(g => g.Key == SpinWheelEngine.Key);
        using var room = RefereeRoom(wheel, 2);
        var spinner = ((WheelPayload)(await room.StartAsync()).GamePayload).Spinner!;

        await room.ActAsync(spinner, "spin");
        var awarded = await room.ActAsync(Referee, "award");

        var payload = (WheelPayload)awarded.GamePayload;
        Assert.Equal("host", payload.AwardMode);
        Assert.Equal(1, payload.Scoreboard.Single(s => s.Player == spinner).Score);
    }

    // Forbidden Words is the one game where a referee sees hidden information: the card, so the referee can judge.
    [Fact]
    public async Task Forbidden_words_referee_sees_the_card_and_guessers_do_not()
    {
        var forbidden = Games.Single(g => g.Key == ForbiddenWordsEngine.Key);
        using var room = RefereeRoom(forbidden, 3);
        await room.StartAsync();

        var referee = await room.PayloadAsync<ForbiddenPayload>(Referee);
        var guesserName = Others.Take(3).Single(p => p != referee.Describer && p != referee.Judge);
        var guesser = await room.PayloadAsync<ForbiddenPayload>(guesserName);
        var publicView = (ForbiddenPayload)(await room.PublicSnapshotAsync()).GamePayload;

        Assert.Equal("referee", referee.Role);
        Assert.NotNull(referee.Card);
        Assert.Null(guesser.Card);
        Assert.Null(publicView.Card);
    }

    [Fact]
    public async Task Forbidden_words_referee_can_flag_a_slip_and_it_is_shown()
    {
        var forbidden = Games.Single(g => g.Key == ForbiddenWordsEngine.Key);
        using var room = RefereeRoom(forbidden, 3);
        await room.StartAsync();

        var flagged = await room.ActAsync(Referee, "flag");

        var payload = (ForbiddenPayload)flagged.GamePayload;
        Assert.Equal("The referee flagged a slip", payload.HostScoring.Single().Reason);
    }
}
