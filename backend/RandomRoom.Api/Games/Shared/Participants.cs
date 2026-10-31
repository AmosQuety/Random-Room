using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;

namespace RandomRoom.Api.Games.Shared;

/// <summary>
/// Who takes part in a room's games. A participant is a seat that plays: every seat, minus the host when the host
/// referees. Game logic (turns, counts, scoreboards) uses participants; seat management uses RoomPlayers directly.
/// </summary>
public static class Participants
{
    public static Task<List<string>> ParticipantNamesAsync(this RoomDbContext db, Guid roomId, CancellationToken ct) =>
        Query(db, roomId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Name).Select(p => p.Name).ToListAsync(ct);

    public static Task<int> ParticipantCountAsync(this RoomDbContext db, Guid roomId, CancellationToken ct) =>
        Query(db, roomId).CountAsync(ct);

    /// <summary>How many seats play, given the seat count: the referee's seat does not.</summary>
    public static int CountAmong(int seats, bool hostPlays) => hostPlays ? seats : seats - 1;

    private static IQueryable<Domain.RoomPlayer> Query(RoomDbContext db, Guid roomId) =>
        db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId
            && !db.Rooms.Any(r => r.Id == roomId && !r.HostPlays && r.HostPlayer == p.Name));
}
