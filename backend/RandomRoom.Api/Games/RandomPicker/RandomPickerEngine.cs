using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.RandomPicker;

/// <summary>
/// The original game: each player triggers one server-random pick from the room's choices.
/// Choices live on the Room (set at room creation) rather than per-session, since this game
/// has no per-session setup - every session in the room picks from the same list.
/// </summary>
public sealed class RandomPickerEngine(RoomDbContext db, IRandomChoiceSource randomSource, TimeProvider clock) : IGameEngine
{
    public const string Key = "random-picker";

    // Label length matches the RoomChoice column; the count matches the setup form's limit.
    private const int MaxChoiceLength = 80;
    private const int MaxChoices = 50;

    public string GameType => Key;

    public async Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct)
    {
        if (!setup.TryGetProperty("choices", out var choicesElement) || choicesElement.ValueKind != JsonValueKind.Array)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Random Picker needs a 'choices' list.");

        var choices = choicesElement.EnumerateArray()
            .Select(e => e.GetString()?.Trim() ?? "")
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
        if (choices.Count < 2)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Random Picker needs at least 2 choices.");
        if (choices.Count > MaxChoices)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Random Picker allows at most {MaxChoices} choices.");
        if (choices.Any(c => c.Length > MaxChoiceLength))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Each choice can be at most {MaxChoiceLength} characters.");

        db.RoomChoices.AddRange(choices.Select((label, i) => new RoomChoice
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Label = label,
            Position = i,
        }));
        await Task.CompletedTask;
    }

    // Nothing to set up per session: choices are room-level and already exist by the time a session starts.
    public Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct) => Task.CompletedTask;

    public async Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct)
    {
        if (action != "trigger")
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Random Picker has no '{action}' action.");

        var alreadyWent = await db.RandomPickerEvents.AnyAsync(e => e.SessionId == sessionId && e.TriggeredBy == actor, ct);
        if (alreadyWent)
            throw new RoomRuleException(RuleViolation.Conflict, "You have already triggered the randomizer this round.");

        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == roomId).OrderBy(c => c.Position).ToListAsync(ct);
        var choice = choices[randomSource.PickIndex(choices.Count)];

        db.RandomPickerEvents.Add(new RandomPickerEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            TriggeredBy = actor,
            Result = choice.Label,
            Timestamp = clock.GetUtcNow(),
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RoomRuleException(RuleViolation.Conflict, "You have already triggered the randomizer this round.");
        }
    }

    public async Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await db.GameSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, ct);
        var playerCount = await db.RoomPlayers.CountAsync(p => p.RoomId == session.RoomId, ct);
        var eventCount = await db.RandomPickerEvents.CountAsync(e => e.SessionId == sessionId, ct);
        return eventCount >= playerCount;
    }

    public async Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct)
    {
        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == roomId).OrderBy(c => c.Position).Select(c => c.Label).ToListAsync(ct);
        var events = await db.RandomPickerEvents.AsNoTracking().Where(e => e.SessionId == sessionId).ToListAsync(ct);

        var players = events.ToDictionary(e => e.TriggeredBy, e => new RandomPickerPlayerState(true, e.Result));
        var tally = choices.Select(c => new TallyView(c, events.Count(e => e.Result == c))).ToList();
        var activity = await db.RandomPickerEvents.AsNoTracking()
            .Where(e => e.Session!.RoomId == roomId)
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .Select(e => new ActivityView(e.Id, e.SessionId, e.Session!.Number, e.TriggeredBy, e.Result, e.Timestamp))
            .ToListAsync(ct);

        return new RandomPickerPayload(choices, players, tally, activity);
    }

    public async Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct)
    {
        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == roomId).OrderBy(c => c.Position).Select(c => c.Label).ToListAsync(ct);
        return new RandomPickerPreview(choices);
    }
}
