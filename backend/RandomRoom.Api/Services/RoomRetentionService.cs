using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RandomRoom.Api.Data;

namespace RandomRoom.Api.Services;

/// <summary>
/// Deletes rooms nobody has touched for <see cref="RoomOptions.RetentionDays"/> days, with everything in them
/// (players, games, answers, audit records), so the database does not grow for ever. A room counts as active when
/// it was created, a game was created, started or ended in it, or a player did something in a game within that window.
/// </summary>
public sealed class RoomRetentionService(RoomDbContext db, TimeProvider clock, IOptions<RoomOptions> options, ILogger<RoomRetentionService> logger)
{
    private const int BatchSize = 100;

    /// <returns>How many rooms were deleted.</returns>
    public async Task<int> DeleteInactiveRoomsAsync(CancellationToken ct = default)
    {
        var days = options.Value.RetentionDays;
        if (days <= 0) return 0;

        var cutoff = clock.GetUtcNow().AddDays(-days);
        var deleted = 0;
        while (true)
        {
            var batch = await InactiveRoomIdsAsync(cutoff, ct);
            if (batch.Count == 0) break;
            await RoomDeletion.DeleteAsync(db, batch, ct);
            deleted += batch.Count;
        }

        if (deleted > 0)
            logger.LogInformation("Deleted {RoomCount} rooms with no activity since {Cutoff}", deleted, cutoff);
        return deleted;
    }

    private Task<List<Guid>> InactiveRoomIdsAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        db.Rooms
            .Where(r => r.CreatedAt < cutoff)
            .Where(r => !db.GameSessions.Any(s => s.RoomId == r.Id && (s.CreatedAt >= cutoff || s.StartedAt >= cutoff || s.EndedAt >= cutoff)))
            .Where(r => !db.GameEntries.Any(e => e.ServerTime >= cutoff && db.GameSessions.Any(s => s.Id == e.SessionId && s.RoomId == r.Id)))
            .Where(r => !db.TriviaAnswers.Any(a => a.Timestamp >= cutoff && db.GameSessions.Any(s => s.Id == a.SessionId && s.RoomId == r.Id)))
            .Where(r => !db.RandomPickerEvents.Any(e => e.Timestamp >= cutoff && db.GameSessions.Any(s => s.Id == e.SessionId && s.RoomId == r.Id)))
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .Take(BatchSize)
            .ToListAsync(ct);
}

/// <summary>Runs the retention clean-up shortly after start and then once a day. A failure is logged and retried next time.</summary>
public sealed class RoomRetentionWorker(IServiceScopeFactory scopes, ILogger<RoomRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RoomRetentionService>().DeleteInactiveRoomsAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Deleting inactive rooms failed; will try again at the next run");
        }
    }
}
