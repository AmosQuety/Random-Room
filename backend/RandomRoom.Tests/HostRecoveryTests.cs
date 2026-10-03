using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Data;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class RecoveryCodeTests
{
    [Fact]
    public void A_code_is_four_groups_of_four_from_an_alphabet_without_look_alikes()
    {
        var code = RecoveryCode.Generate();

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){3}$", code);
        Assert.DoesNotContain(code, c => "ILOU".Contains(c));
    }

    [Fact]
    public void Codes_do_not_repeat()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => RecoveryCode.Generate()).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Theory]
    [InlineData("7QX4-K9M2-VB3H-T8RD", "7QX4K9M2VB3HT8RD")]
    [InlineData("7qx4 k9m2 vb3h t8rd", "7QX4K9M2VB3HT8RD")]
    [InlineData("  7qx4k9m2vb3ht8rd\n", "7QX4K9M2VB3HT8RD")]
    [InlineData("7QX4-K9M2-VB3H-T8RD.", "7QX4K9M2VB3HT8RD")]
    [InlineData("OIL0-1IlO", "01101110")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void What_a_person_types_is_made_comparable(string? typed, string expected) =>
        Assert.Equal(expected, RecoveryCode.Normalize(typed));

    [Fact]
    public void A_generated_code_survives_being_retyped_sloppily()
    {
        var code = RecoveryCode.Generate();

        Assert.Equal(RecoveryCode.Normalize(code), RecoveryCode.Normalize(code.ToLowerInvariant().Replace("-", " ")));
    }
}

/// <summary>Shared set-up. Joining and recovering both count against the 20 a minute join limit, so each derived class gets its own server.</summary>
public abstract class HostRecoveryApiTests
{
    protected static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected readonly ApiTests.Factory factory;
    protected readonly HttpClient client;

    protected HostRecoveryApiTests(ApiTests.Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    protected async Task<HttpClient> JoinAsync(string slug, string player, string pin)
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(slug, player, pin));
        response.EnsureSuccessStatusCode();
        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>())!;
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joined.Token);
        return authed;
    }

    protected async Task<CreateRoomResult> RoomAsync()
    {
        var response = await client.PostAsJsonAsync("/api/rooms",
            new CreateRoomHttpRequest("Test room", "random-picker", TestSetup.Of(new { choices = new[] { "A", "B" } }), ["Amos", "Lydia"], "Amos"));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreateRoomResult>(JsonOptions))!;
        var host = created.Invites.Single(i => i.Player == "Amos");
        (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{host.InviteToken}", new ClaimInviteRequest("1111"))).EnsureSuccessStatusCode();
        return created;
    }

    /// <summary>Signs the host in. Costs one join, so only the tests that need a signed-in host call it.</summary>
    protected Task<HttpClient> HostAsync(CreateRoomResult created) => JoinAsync(created.Slug, "Amos", "1111");

    protected Task<HttpResponseMessage> RecoverAsync(string slug, string? code) =>
        client.PostAsJsonAsync($"/api/rooms/{slug}/recover", new RecoverHostRequest(code));
}

public sealed class HostRecoveryFlowTests(ApiTests.Factory factory) : HostRecoveryApiTests(factory), IClassFixture<ApiTests.Factory>
{
    [Fact]
    public async Task A_new_room_comes_with_a_recovery_code()
    {
        var created = await RoomAsync();

        Assert.Matches("^[0-9A-Z]{4}(-[0-9A-Z]{4}){3}$", created.RecoveryCode);
    }

    [Fact]
    public async Task The_code_is_stored_only_as_a_hash()
    {
        var created = await RoomAsync();

        using var scope = factory.Services.CreateScope();
        var room = scope.ServiceProvider.GetRequiredService<RoomDbContext>().Rooms.Single(r => r.Slug == created.Slug);
        Assert.NotNull(room.RecoveryCodeHash);
        Assert.DoesNotContain(RecoveryCode.Normalize(created.RecoveryCode), room.RecoveryCodeHash);
    }

    [Fact]
    public async Task A_host_who_forgot_their_pin_gets_back_in_with_the_code_typed_sloppily()
    {
        var created = await RoomAsync();
        var oldHost = await HostAsync(created);
        var typed = created.RecoveryCode.ToLowerInvariant().Replace("-", " ");

        var recovered = await RecoverAsync(created.Slug, typed);

        recovered.EnsureSuccessStatusCode();
        var recovery = (await recovered.Content.ReadFromJsonAsync<RecoverHostResponse>(JsonOptions))!;
        Assert.Equal("Amos", recovery.Player);
        Assert.NotEqual(created.RecoveryCode, recovery.RecoveryCode);

        // The old device is signed out and the seat is empty until the host chooses a new PIN.
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldHost.GetAsync("/api/room")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/join", new JoinRequest(created.Slug, "Amos", "1111"))).StatusCode);
        var preview = (await client.GetFromJsonAsync<RoomPreview>($"/api/rooms/{created.Slug}", JsonOptions))!;
        Assert.False(preview.Players.Single(p => p.Name == "Amos").Claimed);

        (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{recovery.InviteToken}", new ClaimInviteRequest("7777"))).EnsureSuccessStatusCode();
        var host = await JoinAsync(created.Slug, "Amos", "7777");
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/room")).StatusCode);
    }

    [Fact]
    public async Task A_code_works_once_and_is_replaced_by_a_new_one()
    {
        var created = await RoomAsync();
        var first = (await (await RecoverAsync(created.Slug, created.RecoveryCode)).Content.ReadFromJsonAsync<RecoverHostResponse>(JsonOptions))!;

        var reused = await RecoverAsync(created.Slug, created.RecoveryCode);
        var withNew = await RecoverAsync(created.Slug, first.RecoveryCode);

        Assert.Equal(HttpStatusCode.Forbidden, reused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }

    [Fact]
    public async Task The_same_code_used_at_the_same_moment_works_for_exactly_one_request()
    {
        var created = await RoomAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RecoverAsync(created.Slug, created.RecoveryCode)));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task The_host_can_make_a_new_code_and_the_old_one_stops_working()
    {
        var created = await RoomAsync();
        var host = await HostAsync(created);

        var made = await host.PostAsync("/api/room/recovery-code", null);

        made.EnsureSuccessStatusCode();
        var code = (await made.Content.ReadFromJsonAsync<RecoveryCodeResponse>(JsonOptions))!.RecoveryCode;
        Assert.NotEqual(created.RecoveryCode, code);
        Assert.Equal(HttpStatusCode.Forbidden, (await RecoverAsync(created.Slug, created.RecoveryCode)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RecoverAsync(created.Slug, code)).StatusCode);
    }
}

public sealed class HostRecoveryGuardTests(ApiTests.Factory factory) : HostRecoveryApiTests(factory), IClassFixture<ApiTests.Factory>
{
    [Fact]
    public async Task A_wrong_code_changes_nothing()
    {
        var created = await RoomAsync();
        var host = await HostAsync(created);

        var response = await RecoverAsync(created.Slug, "AAAA-AAAA-AAAA-AAAA");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/room")).StatusCode);
        var preview = (await client.GetFromJsonAsync<RoomPreview>($"/api/rooms/{created.Slug}", JsonOptions))!;
        Assert.True(preview.Players.Single(p => p.Name == "Amos").Claimed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_code_is_refused(string? code)
    {
        var created = await RoomAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await RecoverAsync(created.Slug, code)).StatusCode);
    }

    [Fact]
    public async Task A_room_that_does_not_exist_and_a_wrong_code_get_the_same_answer()
    {
        var created = await RoomAsync();

        var unknown = await RecoverAsync("no-such-room", created.RecoveryCode);
        var wrong = await RecoverAsync(created.Slug, "AAAA-AAAA-AAAA-AAAA");

        Assert.Equal(wrong.StatusCode, unknown.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_room_made_before_recovery_codes_existed_cannot_be_recovered_until_the_host_makes_one()
    {
        var created = await RoomAsync();
        var host = await HostAsync(created);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomDbContext>();
            db.Rooms.Single(r => r.Slug == created.Slug).RecoveryCodeHash = null;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await RecoverAsync(created.Slug, created.RecoveryCode)).StatusCode);

        var made = await host.PostAsync("/api/room/recovery-code", null);
        var code = (await made.Content.ReadFromJsonAsync<RecoveryCodeResponse>(JsonOptions))!.RecoveryCode;
        Assert.Equal(HttpStatusCode.OK, (await RecoverAsync(created.Slug, code)).StatusCode);
    }

    [Fact]
    public async Task A_player_cannot_make_a_recovery_code()
    {
        var created = await RoomAsync();
        var lydia = created.Invites.Single(i => i.Player == "Lydia");
        (await client.PostAsJsonAsync($"/api/rooms/{created.Slug}/claim/{lydia.InviteToken}", new ClaimInviteRequest("2222"))).EnsureSuccessStatusCode();
        var asLydia = await JoinAsync(created.Slug, "Lydia", "2222");

        var response = await asLydia.PostAsync("/api/room/recovery-code", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Recovering_and_making_a_code_are_recorded_without_the_code_itself()
    {
        var created = await RoomAsync();
        var host = await HostAsync(created);
        (await host.PostAsync("/api/room/recovery-code", null)).EnsureSuccessStatusCode();
        var made = (await (await host.PostAsync("/api/room/recovery-code", null)).Content.ReadFromJsonAsync<RecoveryCodeResponse>(JsonOptions))!;
        (await RecoverAsync(created.Slug, made.RecoveryCode)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomDbContext>();
        var roomId = db.Rooms.Single(r => r.Slug == created.Slug).Id;
        var events = db.RoomAuditEvents.Where(e => e.RoomId == roomId).OrderBy(e => e.OccurredAt).ToList();
        Assert.Equal(["recovery-code-made", "recovery-code-made", "host-recovered"], events.Select(e => e.Action));
        Assert.Equal("recovery code", events[^1].Actor);
        Assert.All(events, e => Assert.Equal("Amos", e.Target));
        Assert.All(events, e => Assert.DoesNotContain(RecoveryCode.Normalize(made.RecoveryCode), e.Actor + e.Action + e.Target));
    }

    [Fact]
    public async Task The_host_seat_still_cannot_be_reset_by_the_old_route_and_the_message_points_to_the_code()
    {
        var host = await HostAsync(await RoomAsync());

        var response = await host.PostAsync("/api/room/players/Amos/reset-pin", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("recovery code", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Guessing codes is throttled like guessing PINs. Own server so the join budget starts full.</summary>
public sealed class RecoveryRateLimitTests : IClassFixture<ApiTests.Factory>
{
    private readonly HttpClient client;

    public RecoveryRateLimitTests(ApiTests.Factory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Repeated_wrong_codes_are_throttled()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 22; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/rooms/anything/recover", new RecoverHostRequest("AAAA-AAAA-AAAA-AAAA"))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
