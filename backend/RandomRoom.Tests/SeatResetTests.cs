using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

/// <summary>Kept apart from ApiTests so it gets its own server, and with it its own join and create rate-limit budget.</summary>
public sealed class SeatResetTests : IClassFixture<ApiTests.Factory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiTests.Factory factory;
    private readonly HttpClient client;

    public SeatResetTests(ApiTests.Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task ClaimAsync(string slug, PlayerInvite invite, string pin) =>
        (await client.PostAsJsonAsync($"/api/rooms/{slug}/claim/{invite.InviteToken}", new ClaimInviteRequest(pin)))
            .EnsureSuccessStatusCode();

    private async Task<HttpClient> JoinAsync(string slug, string player, string pin)
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(slug, player, pin));
        response.EnsureSuccessStatusCode();
        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>())!;
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joined.Token);
        return authed;
    }

    private async Task<(string Slug, HttpClient Amos, HttpClient Lydia)> CreateClaimAndJoinAsync()
    {
        var response = await client.PostAsJsonAsync("/api/rooms",
            new CreateRoomHttpRequest("Test room", "random-picker", TestSetup.Of(new { choices = new[] { "A", "B" } }), ["Amos", "Lydia"], "Amos"));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreateRoomResult>())!;
        await ClaimAsync(created.Slug, created.Invites.Single(i => i.Player == "Amos"), "1111");
        await ClaimAsync(created.Slug, created.Invites.Single(i => i.Player == "Lydia"), "2222");
        return (created.Slug, await JoinAsync(created.Slug, "Amos", "1111"), await JoinAsync(created.Slug, "Lydia", "2222"));
    }

    [Fact]
    public async Task Host_can_reset_a_seat_and_the_player_sets_a_new_pin_with_the_fresh_link()
    {
        var (slug, amos, lydia) = await CreateClaimAndJoinAsync();

        var reset = await amos.PostAsync("/api/room/players/Lydia/reset-pin", null);

        reset.EnsureSuccessStatusCode();
        var invite = (await reset.Content.ReadFromJsonAsync<PlayerInvite>(JsonOptions))!;
        Assert.Equal("Lydia", invite.Player);
        Assert.False(string.IsNullOrEmpty(invite.InviteToken));

        // Everyone can see the seat is empty again, and the old PIN no longer works.
        var preview = (await client.GetFromJsonAsync<RoomPreview>($"/api/rooms/{slug}", JsonOptions))!;
        Assert.False(preview.Players.Single(p => p.Name == "Lydia").Claimed);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/join", new JoinRequest(slug, "Lydia", "2222"))).StatusCode);

        await ClaimAsync(slug, invite, "9999");
        var lydiaAgain = await JoinAsync(slug, "Lydia", "9999");
        Assert.Equal(HttpStatusCode.OK, (await lydiaAgain.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await amos.GetAsync("/api/room")).StatusCode);
        Assert.NotNull(lydia);
    }

    [Fact]
    public async Task A_reset_signs_the_old_device_out_straight_away_even_though_its_token_has_not_expired()
    {
        var (_, amos, lydia) = await CreateClaimAndJoinAsync();
        Assert.Equal(HttpStatusCode.OK, (await lydia.GetAsync("/api/room")).StatusCode);

        (await amos.PostAsync("/api/room/players/Lydia/reset-pin", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await lydia.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await lydia.PostAsJsonAsync("/api/room/action", new ActionRequest("trigger", null))).StatusCode);
    }

    [Fact]
    public async Task A_player_cannot_reset_a_seat()
    {
        var (_, amos, lydia) = await CreateClaimAndJoinAsync();

        var response = await lydia.PostAsync("/api/room/players/Amos/reset-pin", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await amos.GetAsync("/api/room")).StatusCode);
    }

    [Fact]
    public async Task The_hosts_own_seat_cannot_be_reset()
    {
        var (_, amos, _) = await CreateClaimAndJoinAsync();

        var response = await amos.PostAsync("/api/room/players/Amos/reset-pin", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await amos.GetAsync("/api/room")).StatusCode);
    }

    [Fact]
    public async Task A_seat_cannot_be_reset_while_a_game_is_running()
    {
        var (_, amos, lydia) = await CreateClaimAndJoinAsync();
        (await amos.PostAsync("/api/room/session/start", null)).EnsureSuccessStatusCode();

        var response = await amos.PostAsync("/api/room/players/Lydia/reset-pin", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lydia.GetAsync("/api/room")).StatusCode);
    }

    [Fact]
    public async Task Resetting_a_seat_that_does_not_exist_is_not_found()
    {
        var (_, amos, _) = await CreateClaimAndJoinAsync();

        var response = await amos.PostAsync("/api/room/players/Nobody/reset-pin", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_reset_is_recorded_with_who_did_it_to_whom_and_when()
    {
        var (slug, amos, _) = await CreateClaimAndJoinAsync();

        (await amos.PostAsync("/api/room/players/Lydia/reset-pin", null)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RandomRoom.Api.Data.RoomDbContext>();
        var roomId = db.Rooms.Single(r => r.Slug == slug).Id;
        var audit = db.RoomAuditEvents.Single(e => e.RoomId == roomId);
        Assert.Equal(("Amos", "seat-reset", "Lydia"), (audit.Actor, audit.Action, audit.Target));
        Assert.True(audit.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

}
