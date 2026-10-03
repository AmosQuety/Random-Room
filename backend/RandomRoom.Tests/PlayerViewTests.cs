using RandomRoom.Api.Games.RandomPicker;

namespace RandomRoom.Tests;

public class PlayerViewTests
{
    private static GameHarness Picker() =>
        new(d => new RandomPickerEngine(d.Db, d.Random, d.Clock), RandomPickerEngine.Key, new { choices = new[] { "Sarah", "Judith" } });

    [Fact]
    public async Task Snapshot_says_which_seats_have_not_been_claimed_yet()
    {
        using var h = Picker();
        var amos = h.Db.RoomPlayers.Single(p => p.RoomId == h.RoomId && p.Name == "Amos");
        amos.ClaimedAt = DateTimeOffset.UtcNow;
        await h.Db.SaveChangesAsync();

        var players = (await h.SnapshotAsync("Amos")).Players;

        Assert.True(players.Single(p => p.Name == "Amos").Claimed);
        Assert.All(players.Where(p => p.Name != "Amos"), p => Assert.False(p.Claimed));
    }
}
