using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public sealed class ApiTests : IClassFixture<ApiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly TestDatabase database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
            builder.UseSetting("Room:JwtSigningKey", "test-only-signing-key-0123456789-abcdef");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            database.Dispose();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly Factory factory;
    private readonly HttpClient client;

    public ApiTests(Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task<CreateRoomResult> CreateRoomAsync(string title = "Test room")
    {
        var response = await client.PostAsJsonAsync("/api/rooms",
            new CreateRoomHttpRequest(title, ["A", "B"], ["Amos", "Lydia"], "Amos"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateRoomResult>())!;
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
        var created = await CreateRoomAsync();
        await ClaimAsync(created.Slug, created.Invites.Single(i => i.Player == "Amos"), "1111");
        await ClaimAsync(created.Slug, created.Invites.Single(i => i.Player == "Lydia"), "2222");

        var amos = await JoinAsync(created.Slug, "Amos", "1111");
        var lydia = await JoinAsync(created.Slug, "Lydia", "2222");
        return (created.Slug, amos, lydia);
    }

    [Fact]
    public async Task Room_requires_authentication()
    {
        var response = await client.GetAsync("/api/room");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Created_room_can_be_previewed_without_authentication()
    {
        var created = await CreateRoomAsync("Which game tonight?");

        var response = await client.GetAsync($"/api/rooms/{created.Slug}");

        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<RoomPreview>();
        Assert.Equal("Which game tonight?", preview!.Title);
        Assert.All(preview.Players, p => Assert.False(p.Claimed));
    }

    [Fact]
    public async Task Unclaimed_player_cannot_join_until_they_set_a_pin()
    {
        var created = await CreateRoomAsync();

        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(created.Slug, "Amos", "1111"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claiming_the_same_invite_twice_is_rejected()
    {
        var created = await CreateRoomAsync();
        var invite = created.Invites.Single(i => i.Player == "Amos");
        await ClaimAsync(created.Slug, invite, "1111");

        var second = await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{invite.InviteToken}", new ClaimInviteRequest("2222"));

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Theory]
    [InlineData("Amos", "wrong")]
    [InlineData("Mallory", "1111")]
    [InlineData("", "")]
    public async Task Join_rejects_bad_credentials_with_the_same_generic_answer(string player, string pin)
    {
        var created = await CreateRoomAsync();
        await ClaimAsync(created.Slug, created.Invites.Single(i => i.Player == "Amos"), "1111");

        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(created.Slug, player, pin));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_rejects_an_unknown_room_slug()
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest("does-not-exist", "Amos", "1111"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_host_cannot_start_round_over_http()
    {
        var (_, _, lydia) = await CreateClaimAndJoinAsync();

        var response = await lydia.PostAsync("/api/room/session/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Two_rooms_do_not_see_each_others_state()
    {
        var (_, amosA, _) = await CreateClaimAndJoinAsync();
        var (_, amosB, _) = await CreateClaimAndJoinAsync();

        await amosA.PostAsync("/api/room/session/start", null);

        var snapshotB = await amosB.GetFromJsonAsync<RoomSnapshot>("/api/room", JsonOptions);
        Assert.Equal("Waiting", snapshotB!.Session.Status.ToString());
    }
}
