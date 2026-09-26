using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RandomRoom.Api.Data;
using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Services;

/// <summary>
/// The authoritative room: every rule is enforced here, so the UI never decides who may do what.
/// Mutating methods return the new snapshot so callers can broadcast it.
/// </summary>
public sealed class RoomService(
    RoomDbContext db,
    IRandomChoiceSource randomSource,
    PresenceTracker presence,
    IOptions<RoomOptions> options,
    TimeProvider clock)
{
    private const int ActivityLimit = 50;

    private string HostPlayer => options.Value.HostPlayer;

    public async Task EnsureFirstRoundAsync(CancellationToken ct = default)
    {
        if (await db.Rounds.AnyAsync(ct)) return;
        db.Rounds.Add(NewRound(number: 1, RoundStatus.Waiting));
        await db.SaveChangesAsync(ct);
    }

    public async Task<RoomSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var round = await CurrentRoundAsync(ct);
        var roundEvents = await db.RandomEvents.AsNoTracking().Where(e => e.RoundId == round.Id).ToListAsync(ct);
        var activity = await LoadActivityAsync(ct);

        return new RoomSnapshot(
            RoomDefinition.Slug,
            RoomDefinition.Name,
            HostPlayer,
            RoomDefinition.Choices,
            new RoundView(round.Id, round.Number, round.Status, round.StartedAt, round.EndedAt),
            RoomDefinition.Players.Select(p => ToPlayerView(p, roundEvents)).ToList(),
            RoomDefinition.Choices.Select(c => new TallyView(c.Name, roundEvents.Count(e => e.Result == c.Name))).ToList(),
            activity);
    }

    public async Task<RoomSnapshot> TriggerRandomAsync(string player, CancellationToken ct = default)
    {
        if (!RoomDefinition.IsPlayer(player))
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the fixed players can trigger a decision.");

        var round = await CurrentRoundAsync(ct);
        if (round.Status != RoundStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "The round is not active.");

        await RecordEventAsync(round, player, ct);
        await CompleteRoundIfEveryoneWentAsync(round, ct);
        return await GetSnapshotAsync(ct);
    }

    public async Task<RoomSnapshot> StartRoundAsync(string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(actor, ct);
        if (round.Status != RoundStatus.Waiting)
            throw new RoomRuleException(RuleViolation.Conflict, "Only a waiting round can be started.");

        Activate(round);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotAsync(ct);
    }

    public async Task<RoomSnapshot> EndRoundAsync(string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(actor, ct);
        if (round.Status != RoundStatus.Active)
            throw new RoomRuleException(RuleViolation.Conflict, "Only an active round can be ended.");

        Complete(round);
        await db.SaveChangesAsync(ct);
        return await GetSnapshotAsync(ct);
    }

    public async Task<RoomSnapshot> StartNewRoundAsync(string actor, CancellationToken ct = default)
    {
        var round = await CurrentRoundForHostAsync(actor, ct);
        if (round.Status != RoundStatus.Completed)
            throw new RoomRuleException(RuleViolation.Conflict, "End the current round before starting a new one.");

        var next = NewRound(round.Number + 1, RoundStatus.Waiting);
        Activate(next);
        db.Rounds.Add(next);
        await SaveOrTranslateRaceAsync(ct);
        return await GetSnapshotAsync(ct);
    }

    private async Task RecordEventAsync(Round round, string player, CancellationToken ct)
    {
        var choice = RoomDefinition.Choices[randomSource.PickIndex(RoomDefinition.Choices.Count)];
        db.RandomEvents.Add(new RandomEvent
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            TriggeredBy = player,
            Result = choice.Name,
            Timestamp = clock.GetUtcNow(),
        });
        await SaveOrTranslateRaceAsync(ct, "You have already triggered the randomizer this round.");
    }

    private async Task CompleteRoundIfEveryoneWentAsync(Round round, CancellationToken ct)
    {
        var count = await db.RandomEvents.CountAsync(e => e.RoundId == round.Id, ct);
        if (count < RoomDefinition.Players.Count || round.Status == RoundStatus.Completed) return;

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

    private Round NewRound(int number, RoundStatus status) => new()
    {
        Id = Guid.NewGuid(),
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

    private async Task<Round> CurrentRoundAsync(CancellationToken ct) =>
        await db.Rounds.OrderByDescending(r => r.Number).FirstAsync(ct);

    private async Task<Round> CurrentRoundForHostAsync(string actor, CancellationToken ct)
    {
        if (actor != HostPlayer)
            throw new RoomRuleException(RuleViolation.Forbidden, "Only the host can control rounds.");
        return await CurrentRoundAsync(ct);
    }

    private PlayerView ToPlayerView(string player, List<RandomEvent> roundEvents)
    {
        var result = roundEvents.FirstOrDefault(e => e.TriggeredBy == player)?.Result;
        return new PlayerView(player, presence.IsOnline(player), result is not null, result);
    }

    private async Task<List<ActivityView>> LoadActivityAsync(CancellationToken ct) =>
        await db.RandomEvents.AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(ActivityLimit)
            .Select(e => new ActivityView(e.Id, RoomDefinition.Slug, e.RoundId, e.Round!.Number, e.TriggeredBy, e.Result, e.Timestamp))
            .ToListAsync(ct);
}
