using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;

namespace RandomRoom.Api.Services;

/// <summary>
/// Removes whole rooms with everything in them. Used by the retention job and by a host deleting their own room,
/// so there is one definition of what "delete a room" means.
/// </summary>
public static class RoomDeletion
{
    public static async Task DeleteAsync(RoomDbContext db, IReadOnlyCollection<Guid> roomIds, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Recorded answers and picks refuse to be deleted along with their game (they are append-only while the room
        // lives), so they go first. ExecuteDelete bypasses the context's immutability guard on purpose: this is the
        // one place that is allowed to remove them, and only for whole rooms.
        await db.TriviaAnswers.Where(a => db.GameSessions.Any(s => s.Id == a.SessionId && roomIds.Contains(s.RoomId))).ExecuteDeleteAsync(ct);
        await db.RandomPickerEvents.Where(e => db.GameSessions.Any(s => s.Id == e.SessionId && roomIds.Contains(s.RoomId))).ExecuteDeleteAsync(ct);

        // Everything else hangs off the room with cascading deletes.
        await db.Rooms.Where(r => roomIds.Contains(r.Id)).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
