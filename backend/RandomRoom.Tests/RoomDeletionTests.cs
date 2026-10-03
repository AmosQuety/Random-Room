using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>Own server (and so its own rate-limit budget), like SeatResetTests.</summary>
public sealed class RoomDeletionTests : IClassFixture<ApiTests.Factory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiTests.Factory factory;
    private readonly HttpClient client;

    public RoomDeletionTests(ApiTests.Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task<HttpClient> JoinAsync(string slug, string player, string pin)
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(slug, player, pin));
        response.EnsureSuccessStatusCode();
        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>())!;
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joined.Token);
        return authed;
    }

    private async Task<(string Slug, HttpClient Amos, HttpClient Lydia)> RoomAsync()
    {
        var response = await client.PostAsJsonAsync("/api/rooms",
            new CreateRoomHttpRequest("Test room", "random-picker", TestSetup.Of(new { choices = new[] { "A", "B" } }), ["Amos", "Lydia"], "Amos"));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreateRoomResult>())!;
        foreach (var (invite, pin) in new[] { (created.Invites.Single(i => i.Player == "Amos"), "1111"), (created.Invites.Single(i => i.Player == "Lydia"), "2222") })
            (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{invite.InviteToken}", new ClaimInviteRequest(pin))).EnsureSuccessStatusCode();
        return (created.Slug, await JoinAsync(created.Slug, "Amos", "1111"), await JoinAsync(created.Slug, "Lydia", "2222"));
    }

    [Fact]
    public async Task The_host_can_delete_the_room_and_nobody_can_get_back_in()
    {
        var (slug, amos, lydia) = await RoomAsync();

        var deleted = await amos.DeleteAsync("/api/room");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/rooms/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await amos.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await lydia.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/join", new JoinRequest(slug, "Lydia", "2222"))).StatusCode);
    }

    [Fact]
    public async Task A_player_cannot_delete_the_room()
    {
        var (slug, amos, lydia) = await RoomAsync();

        var response = await lydia.DeleteAsync("/api/room");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await amos.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/rooms/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_room_leaves_other_rooms_alone()
    {
        var (_, amosA, _) = await RoomAsync();
        var (slugB, amosB, _) = await RoomAsync();

        (await amosA.DeleteAsync("/api/room")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await amosB.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/rooms/{slugB}")).StatusCode);
    }

    [Fact]
    public async Task A_room_with_a_game_in_progress_and_recorded_picks_can_still_be_deleted()
    {
        var (slug, amos, lydia) = await RoomAsync();
        (await amos.PostAsync("/api/room/session/start", null)).EnsureSuccessStatusCode();
        (await lydia.PostAsJsonAsync("/api/room/action", new ActionRequest("trigger", null))).EnsureSuccessStatusCode();

        var deleted = await amos.DeleteAsync("/api/room");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/rooms/{slug}")).StatusCode);
    }

    [Fact]
    public async Task The_app_says_how_long_rooms_are_kept_before_any_room_exists_and_on_a_rooms_join_page()
    {
        var config = await client.GetFromJsonAsync<AppConfig>("/api/config", JsonOptions);
        Assert.Equal(30, config!.RetentionDays);

        var (slug, _, _) = await RoomAsync();
        var preview = (await client.GetFromJsonAsync<RoomPreview>($"/api/rooms/{slug}", JsonOptions))!;
        Assert.Equal(30, preview.RetentionDays);
    }
}

public class RoomDeletedAnnouncementTests
{
    [Fact]
    public async Task Announcing_a_deleted_room_notifies_it_and_forgets_its_connections_but_not_other_rooms()
    {
        using var h = new GameHarness(d => new SecretEngine(d.Store), "test-secret", new { });
        var presence = new PresenceTracker();
        var otherRoom = Guid.NewGuid();
        presence.Connected("c1", h.RoomId, "Amos");
        presence.Connected("c2", h.RoomId, "Lydia");
        presence.Connected("c3", otherRoom, "Someone");
        var notifier = new RecordingNotifier();

        await new RoomBroadcaster(h.Service, presence, notifier).AnnounceRoomDeletedAsync(h.RoomId);

        Assert.Equal([h.RoomId], notifier.DeletedRooms);
        Assert.Empty(presence.OnlinePlayers(h.RoomId));
        Assert.True(presence.IsOnline(otherRoom, "Someone"));
    }
}
