using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RandomRoom.Api.Endpoints;

namespace RandomRoom.Tests;

public sealed class ApiTests : IClassFixture<ApiTests.Factory>
{
    private static readonly Dictionary<string, string> Pins =
        new() { ["Amos"] = "1111", ["Lydia"] = "2222", ["James"] = "3333", ["Jacob"] = "4444" };

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly TestDatabase database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
            builder.UseSetting("Room:JwtSigningKey", "test-only-signing-key-0123456789-abcdef");
            foreach (var (player, pin) in Pins) builder.UseSetting($"Room:PlayerPins:{player}", pin);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            database.Dispose();
        }
    }

    private readonly Factory factory;
    private readonly HttpClient client;

    public ApiTests(Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task<HttpClient> JoinAs(string player, string pin)
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(player, pin));
        response.EnsureSuccessStatusCode();
        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>())!;
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joined.Token);
        return authed;
    }

    [Fact]
    public async Task Room_requires_authentication()
    {
        var response = await client.GetAsync("/api/room");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Amos", "wrong")]
    [InlineData("Mallory", "1111")]
    [InlineData("", "")]
    public async Task Join_rejects_bad_credentials_with_the_same_generic_answer(string player, string pin)
    {
        var response = await client.PostAsJsonAsync("/api/join", new JoinRequest(player, pin));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_host_cannot_start_round_over_http()
    {
        var lydia = await JoinAs("Lydia", "2222");

        var response = await lydia.PostAsync("/api/room/round/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
