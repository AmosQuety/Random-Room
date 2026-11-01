using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Data;

namespace RandomRoom.Api.Games.Shared;

/// <summary>
/// Who takes part in a room's games. A participant is a seat that plays: every seat, minus the host when the host
/// referees. Game logic (turns, counts, scoreboards) uses participants; seat management uses RoomPlayers directly.
/// "Whether the host plays" is the room's latest session's flag, so a toggle between games never changes a finished game.
/// </summary>
public static class Participants
{
    public static Task<List<string>> ParticipantNamesAsync(this RoomDbContext db, Guid roomId, CancellationToken ct) =>
        Query(db, roomId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Name).Select(p => p.Name).ToListAsync(ct);

    public static Task<int> ParticipantCountAsync(this RoomDbContext db, Guid roomId, CancellationToken ct) =>
        Query(db, roomId).CountAsync(ct);

    /// <summary>How many seats play, given the seat count: the referee's seat does not.</summary>
    public static int CountAmong(int seats, bool hostPlays) => hostPlays ? seats : seats - 1;

    /// <summary>Whether the host plays in the room's latest session.</summary>
    public static IQueryable<bool> LatestSessionHostPlays(this RoomDbContext db, Guid roomId) =>
        db.GameSessions.AsNoTracking().Where(s => s.RoomId == roomId).OrderByDescending(s => s.Number).Select(s => s.HostPlays).Take(1);

    private static IQueryable<Domain.RoomPlayer> Query(RoomDbContext db, Guid roomId) =>
        db.RoomPlayers.AsNoTracking().Where(p => p.RoomId == roomId
            && !(db.Rooms.Any(r => r.Id == roomId && r.HostPlayer == p.Name) && db.LatestSessionHostPlays(roomId).Any(plays => !plays)));
}
