using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RandomRoom.Api.Data;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>A referee room over HTTP, and the HostPlays migration on a database that already holds a room.</summary>
public sealed class RefereeApiTests(ApiTests.Factory factory) : IClassFixture<ApiTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private async Task<HttpClient> CreateAndJoinAsHostAsync(bool? hostPlays)
    {
        var response = await client.PostAsJsonAsync("/api/rooms", new CreateRoomHttpRequest(
            "Referee room", "random-picker", TestSetup.Of(new { choices = new[] { "A", "B" } }), ["Rita", "Lydia", "James"], "Rita", hostPlays));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreateRoomResult>())!;
        var invite = created.Invites.Single(i => i.Player == "Rita");
        (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{invite.InviteToken}", new ClaimInviteRequest("1111"))).EnsureSuccessStatusCode();
        var join = await client.PostAsJsonAsync("/api/join", new JoinRequest(created.Slug, "Rita", "1111"));
        join.EnsureSuccessStatusCode();
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await join.Content.ReadFromJsonAsync<JoinResponse>())!.Token);
        return authed;
    }

    private static async Task<JsonElement> SnapshotAsync(HttpClient host) =>
        await host.GetFromJsonAsync<JsonElement>("/api/room");

    [Fact]
    public async Task Room_created_with_hostPlays_false_says_so_in_the_snapshot()
    {
        var host = await CreateAndJoinAsHostAsync(hostPlays: false);

        var snapshot = await SnapshotAsync(host);

        Assert.False(snapshot.GetProperty("hostPlays").GetBoolean());
        var plays = snapshot.GetProperty("players").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("plays").GetBoolean());
        Assert.False(plays["Rita"]);
        Assert.True(plays["Lydia"]);
    }

    [Fact]
    public async Task Room_created_without_hostPlays_keeps_a_host_who_plays()
    {
        var host = await CreateAndJoinAsHostAsync(hostPlays: null);

        var snapshot = await SnapshotAsync(host);

        Assert.True(snapshot.GetProperty("hostPlays").GetBoolean());
    }

    [Fact]
    public async Task Referee_play_action_is_a_403()
    {
        var host = await CreateAndJoinAsHostAsync(hostPlays: false);
        (await host.PostAsync("/api/room/session/start", null)).EnsureSuccessStatusCode();

        var response = await host.PostAsJsonAsync("/api/room/action", new ActionRequest("trigger", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("refereeing", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Migration_backfills_existing_rooms_as_host_plays()
    {
        using var database = new TestDatabase();
        await using var db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003200247_AddRoomRecoveryCode");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Rooms\" (\"Id\", \"Slug\", \"Title\", \"GameType\", \"HostPlayer\", \"CreatedAt\") VALUES ({0}, 'oldroom', 'Old room', 'random-picker', 'Amos', now())",
            Guid.NewGuid());

        await migrator.MigrateAsync();

        Assert.True(await db.Rooms.AsNoTracking().Where(r => r.Slug == "oldroom").Select(r => r.HostPlays).SingleAsync());
    }

    [Fact]
    public async Task Host_plays_without_a_value_is_a_400_and_changes_nothing()
    {
        var host = await CreateAndJoinAsHostAsync(hostPlays: false);
        (await host.PostAsync("/api/room/session/start", null)).EnsureSuccessStatusCode();
        (await host.PostAsync("/api/room/session/end", null)).EnsureSuccessStatusCode();
        var roomId = (await SnapshotAsync(host)).GetProperty("roomId").GetGuid();

        var response = await host.PostAsJsonAsync("/api/room/host-plays", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await SnapshotAsync(host)).GetProperty("hostPlays").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomDbContext>();
        Assert.False(await db.RoomAuditEvents.AnyAsync(e => e.RoomId == roomId));
    }

    [Fact]
    public async Task Host_plays_toggle_works_over_http_between_games()
    {
        var host = await CreateAndJoinAsHostAsync(hostPlays: false);
        (await host.PostAsync("/api/room/session/start", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await host.PostAsJsonAsync("/api/room/host-plays", new { plays = true })).StatusCode);
        (await host.PostAsync("/api/room/session/end", null)).EnsureSuccessStatusCode();

        var response = await host.PostAsJsonAsync("/api/room/host-plays", new { plays = true });

        response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hostPlays").GetBoolean());
    }

    [Fact]
    public async Task Migration_gives_each_existing_session_its_room_setting_and_rolls_back()
    {
        using var database = new TestDatabase();
        await using var db = new RoomDbContext(new DbContextOptionsBuilder<RoomDbContext>().UseNpgsql(database.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031114639_AddRoomHostPlays");
        var (refereeRoom, playingRoom) = (Guid.NewGuid(), Guid.NewGuid());
        foreach (var (id, slug, plays) in new[] { (refereeRoom, "refroom", false), (playingRoom, "playroom", true) })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Rooms\" (\"Id\", \"Slug\", \"Title\", \"GameType\", \"HostPlayer\", \"CreatedAt\", \"HostPlays\") VALUES ({0}, {1}, 'Room', 'random-picker', 'Amos', now(), {2})",
                id, slug, plays);
            for (var number = 1; number <= 3; number++)
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"GameSessions\" (\"Id\", \"RoomId\", \"Number\", \"Status\", \"CreatedAt\") VALUES ({0}, {1}, {2}, 'Completed', now())",
                    Guid.NewGuid(), id, number);
        }

        await migrator.MigrateAsync();
        await migrator.MigrateAsync("20261031114639_AddRoomHostPlays");
        await migrator.MigrateAsync();

        var flags = await db.GameSessions.AsNoTracking().GroupBy(s => s.RoomId).Select(g => new { g.Key, Values = g.Select(s => s.HostPlays).Distinct().ToList() }).ToListAsync();
        Assert.Equal([false], flags.Single(f => f.Key == refereeRoom).Values);
        Assert.Equal([true], flags.Single(f => f.Key == playingRoom).Values);
        Assert.Equal(6, await db.GameSessions.CountAsync());
    }
}
