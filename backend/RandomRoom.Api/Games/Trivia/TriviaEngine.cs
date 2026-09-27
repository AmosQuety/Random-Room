using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Trivia;

/// <summary>
/// Host-curated multiple-choice questions, one at a time. Everyone answers the current question;
/// once every room player has answered, the session auto-advances to the next one. Questions are
/// room-level (curated at creation), like Random Picker's choices - every session in the room
/// replays the same set from the start.
/// </summary>
public sealed class TriviaEngine(RoomDbContext db, TimeProvider clock) : IGameEngine
{
    public const string Key = "trivia";

    public string GameType => Key;

    public Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        if (!setup.TryGetProperty("questions", out var questionsElement) || questionsElement.ValueKind != JsonValueKind.Array)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Trivia needs a 'questions' list.");

        var position = 0;
        var questions = new List<TriviaQuestion>();
        foreach (var q in questionsElement.EnumerateArray())
        {
            var text = q.TryGetProperty("text", out var textEl) ? textEl.GetString()?.Trim() ?? "" : "";
            var options = q.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array
                ? optionsEl.EnumerateArray().Select(o => o.GetString()?.Trim() ?? "").Where(o => o.Length > 0).ToList()
                : [];
            var correctIndex = q.TryGetProperty("correctIndex", out var idxEl) && idxEl.TryGetInt32(out var idx) ? idx : -1;

            if (text.Length == 0)
                throw new RoomRuleException(RuleViolation.InvalidInput, "Every trivia question needs text.");
            if (options.Count < 2)
                throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs at least 2 options.");
            if (correctIndex < 0 || correctIndex >= options.Count)
                throw new RoomRuleException(RuleViolation.InvalidInput, $"'{text}' needs a valid correct option.");

            questions.Add(new TriviaQuestion
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                Position = position++,
                Text = text,
                Options = options,
                CorrectIndex = correctIndex,
            });
        }

        if (questions.Count == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Trivia needs at least 1 question.");

        db.TriviaQuestions.AddRange(questions);
        return Task.CompletedTask;
    }

    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        db.TriviaSessionStates.Add(new TriviaSessionState { Id = Guid.NewGuid(), SessionId = sessionId, CurrentQuestionIndex = 0 });
        return Task.CompletedTask;
    }

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action != "answer")
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Trivia has no '{action}' action.");
        if (payload is null || !payload.Value.TryGetProperty("optionIndex", out var optionEl) || !optionEl.TryGetInt32(out var optionIndex))
            throw new RoomRuleException(RuleViolation.InvalidInput, "An answer needs an optionIndex.");

        var state = await db.TriviaSessionStates.SingleAsync(s => s.SessionId == sessionId, ct);
        var questions = await db.TriviaQuestions.AsNoTracking()
            .Where(q => q.RoomId == roomId).OrderBy(q => q.Position).ToListAsync(ct);

        if (state.CurrentQuestionIndex >= questions.Count)
            throw new RoomRuleException(RuleViolation.Conflict, "There is no active question.");

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
        {
            state.CurrentQuestionIndex++;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.AsNoTracking().SingleAsync(s => s.SessionId == sessionId, ct);
        var session = await db.GameSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, ct);
        var totalQuestions = await db.TriviaQuestions.CountAsync(q => q.RoomId == session.RoomId, ct);
        return state.CurrentQuestionIndex >= totalQuestions;
    }

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var state = await db.TriviaSessionStates.AsNoTracking().SingleAsync(s => s.SessionId == sessionId, ct);
        var questions = await db.TriviaQuestions.AsNoTracking()
            .Where(q => q.RoomId == roomId).OrderBy(q => q.Position).ToListAsync(ct);
        var allAnswers = await db.TriviaAnswers.AsNoTracking().Where(a => a.SessionId == sessionId).ToListAsync(ct);
        var players = await db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId).Select(p => p.Name).ToListAsync(ct);

        TriviaQuestionView? current = null;
        var answered = new Dictionary<string, bool>();
        if (state.CurrentQuestionIndex < questions.Count)
        {
            var q = questions[state.CurrentQuestionIndex];
            current = new TriviaQuestionView(q.Text, q.Options);
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

        var activity = allAnswers
            .Join(questions, a => a.QuestionId, q => q.Id, (a, q) => new TriviaActivityView(a.Id, q.Position + 1, a.TriggeredBy, a.OptionIndex == q.CorrectIndex, a.Timestamp))
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
            activity);
    }

    public async Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct) =>
        new TriviaPreview(await db.TriviaQuestions.CountAsync(q => q.RoomId == roomId, ct));
}
