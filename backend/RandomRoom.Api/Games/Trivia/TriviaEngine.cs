using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Trivia;

/// <summary>
/// Host-curated multiple-choice questions, one at a time. Everyone answers the current question;
/// once every room player has answered, the session auto-advances to the next one. Questions are
/// room-level (curated at creation), like Random Picker's choices - every session in the room
/// replays the same set from the start.
///
/// Optional extras, all additive: a category label per question, a built-in starter bank the host can mix in,
/// and a per-question time limit. With a limit, the server closes a question when its own clock passes the
/// deadline (a client "tick" only asks it to check), and players who did not answer simply score nothing.
/// </summary>
public sealed class TriviaEngine(RoomDbContext db, TimeProvider clock, IRandomChoiceSource? random = null) : IGameEngine
{
    public const string Key = "trivia";
    public const string StarterBank = "trivia-starter";
    public const string EastAfricaBank = "trivia-east-africa";

    /// <summary>The ready-made question sets a host can add, by the name the setup form sends.</summary>
    public static readonly IReadOnlyDictionary<string, string> StarterPacks = new Dictionary<string, string>
    {
        ["general"] = StarterBank,
        ["east-africa"] = EastAfricaBank,
    };

    private const string DefaultPack = "general";

    private const int MaxCategoryLength = 40;
    public const int MaxQuestionLength = 300;
    public const int MaxOptionLength = 100;
    public const int MaxOptionsPerQuestion = 6;

    /// <summary>The most questions one room can hold, your own and the starter ones together.</summary>
    public const int MaxQuestions = 50;
    private const int MinTimeLimit = 5;
    private const int MaxTimeLimit = 120;
    private const int DefaultBuiltInCount = 10;

    private readonly IRandomChoiceSource choices = random ?? new CryptoRandomChoiceSource();

    public string GameType => Key;

    // ---- setup ----

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        var useBuiltIn = SetupJson.Flag(setup, "useBuiltIn", fallback: false);
        var hasList = setup.TryGetProperty("questions", out var questionsElement) && questionsElement.ValueKind == JsonValueKind.Array;
        if (!hasList && !useBuiltIn)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Trivia needs a 'questions' list.");

        if (hasList && questionsElement.GetArrayLength() > MaxQuestions)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A trivia game can have at most {MaxQuestions} questions.");

        var questions = hasList ? questionsElement.EnumerateArray().Select(ParseQuestion).ToList() : [];
        if (useBuiltIn)
            questions.AddRange(PickStarterQuestions(setup));
        if (questions.Count > MaxQuestions)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A trivia game can have at most {MaxQuestions} questions, your own and the starter ones together.");

        if (questions.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Trivia needs at least 1 question.");

        var position = 0;
        foreach (var question in questions)
        {
            question.Id = Guid.NewGuid();
            question.RoomId = roomId;
            question.Position = position++;
        }
        db.TriviaQuestions.AddRange(questions);

        var timeLimit = SetupJson.OptionalInt(setup, "timeLimitSeconds", MinTimeLimit, MaxTimeLimit, "The time limit");
        if (timeLimit is not null)
            db.RoomGameSetups.Add(new RoomGameSetup { Id = Guid.NewGuid(), RoomId = roomId, Json = GameStore.Serialize(new TriviaOptions(timeLimit)) });
        return Task.CompletedTask;
    }

    private static TriviaQuestion ParseQuestion(JsonElement q)
    {
        var text = q.TryGetProperty("text", out var textEl) ? textEl.GetString()?.Trim() ?? "" : "";
        var rawOptions = q.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array ? optionsEl : default;
        if (rawOptions.ValueKind == JsonValueKind.Array && rawOptions.GetArrayLength() > MaxOptionsPerQuestion)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A trivia question can have at most {MaxOptionsPerQuestion} options.");

        var options = rawOptions.ValueKind == JsonValueKind.Array
            ? rawOptions.EnumerateArray().Select(o => o.GetString()?.Trim() ?? "").Where(o => o.Length > 0).ToList()
            : [];
        var correctIndex = q.TryGetProperty("correctIndex", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number && idxEl.TryGetInt32(out var idx) ? idx : -1;

        if (text.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Every trivia question needs text.");
        if (text.Length > MaxQuestionLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"A trivia question can be at most {MaxQuestionLength} characters.");
        if (options.Count < 2)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs at least 2 options.");
        if (options.Any(o => o.Length > MaxOptionLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"An option can be at most {MaxOptionLength} characters.");
        if (correctIndex < 0 || correctIndex >= options.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs a valid correct option.");

        var category = SetupJson.OptionalText(q, "category", MaxCategoryLength, "A category");
        return new TriviaQuestion { Text = text, Options = options, CorrectIndex = correctIndex, Category = category.Length == 0 ? null : category };
    }

    private IEnumerable<TriviaQuestion> PickStarterQuestions(JsonElement setup)
    {
        var pack = SetupJson.OptionalText(setup, "starterPack", 32, "The question pack");
        if (pack.Length == 0) pack = DefaultPack;
        if (!StarterPacks.TryGetValue(pack, out var bankName))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"'{pack}' is not a known question pack.");

        var bank = ContentBank.Load<StarterQuestion>(bankName);
        var count = SetupJson.OptionalInt(setup, "builtInCount", 1, bank.Count, "The number of built-in questions") ?? Math.Min(DefaultBuiltInCount, bank.Count);
        return choices.Shuffle(bank).Take(count)
            .Select(b => new TriviaQuestion { Text = b.Text, Options = b.Options.ToList(), CorrectIndex = b.CorrectIndex, Category = b.Category });
    }

    private sealed record StarterQuestion(string Text, List<string> Options, int CorrectIndex, string Category);

    private sealed record TriviaOptions(int? TimeLimitSeconds);

    private async Task<int?> TimeLimitAsync(Guid roomId, CancellationToken ct)
    {
        var row = await db.RoomGameSetups.AsNoTracking().FirstOrDefaultAsync(s => s.RoomId == roomId, ct);
        return row is null ? null : GameStore.Deserialize<TriviaOptions>(row.Json).TimeLimitSeconds;
    }

    // ---- lifecycle ----

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        db.TriviaSessionStates.Add(new TriviaSessionState { Id = Guid.NewGuid(), SessionId = sessionId, CurrentQuestionIndex = 0 });
        return Task.CompletedTask;
    }

    public async Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var limit = await TimeLimitAsync(roomId, ct);
        if (limit is null) return;

        var state = await db.TriviaSessionStates.SingleAsync(s => s.SessionId == sessionId, ct);
        state.DeadlineAt = ServerTimer.DeadlineIn(clock, limit);
        await db.SaveChangesAsync(ct);
    }

    // ---- actions ----

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action is not ("answer" or "tick"))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Trivia has no '{action}' action.");
        if (action == "answer" && (payload is not { ValueKind: JsonValueKind.Object } || !payload.Value.TryGetProperty("optionIndex", out var optionEl) || optionEl.ValueKind != JsonValueKind.Number || !optionEl.TryGetInt32(out _)))
            throw new RoomRuleException(RuleViolation.InvalidInput, "An answer needs an optionIndex.");

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        // Answers, ticks and the auto-advance are applied one at a time, so two of them can never both move the question on.
        await db.TriviaSessionStates
            .FromSql($"SELECT * FROM \"TriviaSessionStates\" WHERE \"SessionId\" = {sessionId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);

        if (action == "tick")
            await TickAsync(roomId, sessionId, ct);
        else
            await AnswerAsync(roomId, sessionId, actor, payload!.Value.GetProperty("optionIndex").GetInt32(), ct);

        if (tx is not null) await tx.CommitAsync(ct);
    }

    private async Task AnswerAsync(Guid roomId, Guid sessionId, string actor, int optionIndex, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.SingleAsync(s => s.SessionId == sessionId, ct);
        var questions = await db.TriviaQuestions.AsNoTracking()
            .Where(q => q.RoomId == roomId).OrderBy(q => q.Position).ToListAsync(ct);

        if (state.CurrentQuestionIndex >= questions.Count)
            throw new RoomRuleException(RuleViolation.Conflict, "There is no active question.");
        if (ServerTimer.HasPassed(clock, state.DeadlineAt))
            throw new RoomRuleException(RuleViolation.Conflict, "Time is up for this question.");

        var current = questions[state.CurrentQuestionIndex];
        if (optionIndex < 0 || optionIndex >= current.Options.Count)
            throw new RoomRuleException(RuleViolation.InvalidInput, "That is not a valid option.");

        var alreadyAnswered = await db.TriviaAnswers
            .AnyAsync(a => a.SessionId == sessionId && a.QuestionId == current.Id && a.TriggeredBy == actor, ct);
        if (alreadyAnswered)
            throw new RoomRuleException(RuleViolation.Conflict, "You have already answered this question.");

        db.TriviaAnswers.Add(new TriviaAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            QuestionId = current.Id,
            TriggeredBy = actor,
            OptionIndex = optionIndex,
            Timestamp = clock.GetUtcNow(),
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RoomRuleException(RuleViolation.Conflict, "You have already answered this question.");
        }

        var playerCount = await db.RoomPlayers.CountAsync(p => p.RoomId == roomId, ct);
        var answeredCount = await db.TriviaAnswers.CountAsync(a => a.SessionId == sessionId && a.QuestionId == current.Id, ct);
        if (answeredCount >= playerCount)
            await AdvanceAsync(roomId, state, questions.Count, ct);
    }

    /// <summary>Closes the question only when the server's own clock says its time is up; any other tick changes nothing.</summary>
    private async Task TickAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.SingleAsync(s => s.SessionId == sessionId, ct);
        var total = await db.TriviaQuestions.CountAsync(q => q.RoomId == roomId, ct);
        if (state.CurrentQuestionIndex >= total || !ServerTimer.HasPassed(clock, state.DeadlineAt)) return;
        await AdvanceAsync(roomId, state, total, ct);
    }

    private async Task AdvanceAsync(Guid roomId, TriviaSessionState state, int totalQuestions, CancellationToken ct)
    {
        state.CurrentQuestionIndex++;
        state.DeadlineAt = state.CurrentQuestionIndex < totalQuestions
            ? ServerTimer.DeadlineIn(clock, await TimeLimitAsync(roomId, ct))
            : null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.AsNoTracking().SingleAsync(s => s.SessionId == sessionId, ct);
        var session = await db.GameSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, ct);
        var totalQuestions = await db.TriviaQuestions.CountAsync(q => q.RoomId == session.RoomId, ct);
        return state.CurrentQuestionIndex >= totalQuestions;
    }

    // ---- views ----

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.AsNoTracking().SingleAsync(s => s.SessionId == sessionId, ct);
        var questions = await db.TriviaQuestions.AsNoTracking()
            .Where(q => q.RoomId == roomId).OrderBy(q => q.Position).ToListAsync(ct);
        var allAnswers = await db.TriviaAnswers.AsNoTracking().Where(a => a.SessionId == sessionId).ToListAsync(ct);
        var players = await db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId).Select(p => p.Name).ToListAsync(ct);
        var timeLimit = await TimeLimitAsync(roomId, ct);

        TriviaQuestionView? current = null;
        var answered = new Dictionary<string, bool>();
        if (state.CurrentQuestionIndex < questions.Count)
        {
            var q = questions[state.CurrentQuestionIndex];
            current = new TriviaQuestionView(q.Text, q.Options, q.Category);
            answered = players.ToDictionary(p => p, p => allAnswers.Any(a => a.QuestionId == q.Id && a.TriggeredBy == p));
        }

        TriviaReveal? lastReveal = null;
        if (state.CurrentQuestionIndex > 0)
        {
            var prev = questions[state.CurrentQuestionIndex - 1];
            var prevAnswers = allAnswers.Where(a => a.QuestionId == prev.Id).ToDictionary(a => a.TriggeredBy, a => a.OptionIndex);
            lastReveal = new TriviaReveal(prev.Text, prev.Options, prev.CorrectIndex, prevAnswers);
        }

        var correctByPlayer = allAnswers
            .Join(questions, a => a.QuestionId, q => q.Id, (a, q) => new { a.TriggeredBy, Correct = a.OptionIndex == q.CorrectIndex })
            .Where(x => x.Correct)
            .GroupBy(x => x.TriggeredBy)
            .ToDictionary(g => g.Key, g => g.Count());
        var scoreboard = players
            .Select(p => new TriviaScore(p, correctByPlayer.GetValueOrDefault(p, 0)))
            .OrderByDescending(s => s.Correct)
            .ToList();

        // Only closed questions: whether an answer was right must not be visible while others can still answer.
        var activity = allAnswers
            .Join(questions.Where(q => q.Position < state.CurrentQuestionIndex), a => a.QuestionId, q => q.Id, (a, q) => new TriviaActivityView(a.Id, q.Position + 1, a.TriggeredBy, a.OptionIndex == q.CorrectIndex, a.Timestamp))
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .ToList();

        return new TriviaPayload(
            Math.Min(state.CurrentQuestionIndex + 1, questions.Count),
            questions.Count,
            current,
            answered,
            lastReveal,
            scoreboard,
            activity,
            timeLimit is null ? null : ServerTimer.View(clock, state.DeadlineAt),
            timeLimit);
    }

    public async Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        new TriviaPreview(await db.TriviaQuestions.CountAsync(q => q.RoomId == roomId, ct));
}
