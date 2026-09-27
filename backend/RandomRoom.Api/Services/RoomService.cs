using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

/// <summary>
/// The authoritative room: every rule is enforced here, so the UI never decides who may do what.
/// Every method is scoped to a single room by its id. Mutating methods return the new snapshot
/// so callers can broadcast it.
/// </summary>
public sealed class RoomService(
    RoomDbContext db,
    IRandomChoiceSource randomSource,
    PresenceTracker presence,
    TimeProvider clock)
{
    private const int ActivityLimit = 50;

    public async Task<RoomSnapshot> GetSnapshotAsync(Guid roomId, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(roomId, ct);
        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == roomId).OrderBy(c => c.Position).ToListAsync(ct);
        var players = await db.RoomPlayers.AsNoTracking()
            .Where(p => p.RoomId == roomId).ToListAsync(ct);
        var round = await CurrentRoundAsync(roomId, ct);
        var roundEvents = await db.RandomEvents.AsNoTracking().Where(e => e.RoundId == round.Id).ToListAsync(ct);
        var activity = await LoadActivityAsync(roomId, ct);

        return new RoomSnapshot(
            room.Id,
            room.Slug,
            room.Title,
            room.HostPlayer,
            choices.Select(c => c.Label).ToList(),
            new RoundView(round.Id, round.Number, round.Status, round.StartedAt, round.EndedAt),
            players.Select(p => ToPlayerView(roomId, p, roundEvents)).ToList(),
            choices.Select(c => new TallyView(c.Label, roundEvents.Count(e => e.Result == c.Label))).ToList(),
            activity);
    }

    public async Task<RoomSnapshot> TriggerRandomAsync(Guid roomId, string player, CancellationToken ct = default)
    {
        var isPlayer = await db.RoomPlayers.AnyAsync(p => p.RoomId == roomId && p.Name == player, ct);
        if (!isPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only players in this room can trigger a decision.");

        var round = await CurrentRoundAsync(roomId, ct);
        if (round.Status != RoundStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "The round is not active.");

        await RecordEventAsync(roomId, round, player, ct);
        await CompleteRoundIfEveryoneWentAsync(roomId, round, ct);
        return await GetSnapshotAsync(roomId, ct);
    }

    public async Task<RoomSnapshot> StartRoundAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(roomId, actor, ct);
        if (round.Status != RoundStatus.Waiting)
            throw new RoomRuleException(RuleViolation.Conflict, "Only a waiting round can be started.");

        Activate(round);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotAsync(roomId, ct);
    }

    public async Task<RoomSnapshot> EndRoundAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(roomId, actor, ct);
        if (round.Status != RoundStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "Only an active round can be ended.");

        Complete(round);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotAsync(roomId, ct);
    }

    public async Task<RoomSnapshot> StartNewRoundAsync(Guid roomId, string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(roomId, actor, ct);
        if (round.Status != RoundStatus.Completed)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current round before starting a new one.");

        var next = NewRound(roomId, round.Number + 1, RoundStatus.Waiting);
        Activate(next);
        db.Rounds.Add(next);
        await SaveOrTranslateRaceAsync(ct);
        return await GetSnapshotAsync(roomId, ct);
    }

    private async Task RecordEventAsync(Guid roomId, Round round, string player, CancellationToken ct)
    {
        var choices = await db.RoomChoices.AsNoTracking()
            .Where(c => c.RoomId == roomId).OrderBy(c => c.Position).ToListAsync(ct);
        var choice = choices[randomSource.PickIndex(choices.Count)];
        db.RandomEvents.Add(new RandomEvent
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            TriggeredBy = player,
            Result = choice.Label,
            Timestamp = clock.GetUtcNow(),
        });
        await SaveOrTranslateRaceAsync(ct, "You have already triggered the randomizer this round.");
    }

    private async Task CompleteRoundIfEveryoneWentAsync(Guid roomId, Round round, CancellationToken ct)
    {
        var playerCount = await db.RoomPlayers.CountAsync(p => p.RoomId == roomId, ct);
        var count = await db.RandomEvents.CountAsync(e => e.RoundId == round.Id, ct);
        if (count < playerCount || round.Status == RoundStatus.Completed) return;

        Complete(round);
        await db.SaveChangesAsync(ct);
    }

    // The unique indexes are the last line of defence against concurrent duplicate requests.
    private async Task SaveOrTranslateRaceAsync(CancellationToken ct, string message = "The room changed; please refresh.")
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RoomRuleException(RuleViolation.Conflict, message);
        }
    }

    private Round NewRound(Guid roomId, int number, RoundStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        Number = number,
        Status = status,
        CreatedAt = clock.GetUtcNow(),
    };

    private void Activate(Round round)
    {
        round.Status = RoundStatus.Active;
        round.StartedAt = clock.GetUtcNow();
    }

    private void Complete(Round round)
    {
        round.Status = RoundStatus.Completed;
        round.EndedAt = clock.GetUtcNow();
    }

    private async Task<Room> RequireRoomAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct)
        ?? throw new RoomRuleException(RuleViolation.NotFound, "Room not found.");

    private async Task<Round> CurrentRoundAsync(Guid roomId, CancellationToken ct) =>
        await db.Rounds.Where(r => r.RoomId == roomId).OrderByDescending(r => r.Number).FirstAsync(ct);

    private async Task<Round> CurrentRoundForHostAsync(Guid roomId, string actor, CancellationToken ct)
    {
        var room = await RequireRoomAsync(roomId, ct);
        if (actor != room.HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control rounds.");
        return await CurrentRoundAsync(roomId, ct);
    }

    private PlayerView ToPlayerView(Guid roomId, RoomPlayer player, List<RandomEvent> roundEvents)
    {
        var result = roundEvents.FirstOrDefault(e => e.TriggeredBy == player.Name)?.Result;
        return new PlayerView(player.Name, presence.IsOnline(roomId, player.Name), result is not null, result);
    }

    private async Task<List<ActivityView>> LoadActivityAsync(Guid roomId, CancellationToken ct) =>
        await db.RandomEvents.AsNoTracking()
            .Where(e => e.Round!.RoomId == roomId)
            .OrderByDescending(e => e.Timestamp)
            .Take(ActivityLimit)
            .Select(e => new ActivityView(e.Id, e.RoundId, e.Round!.Number, e.TriggeredBy, e.Result, e.Timestamp))
            .ToListAsync(ct);
}
