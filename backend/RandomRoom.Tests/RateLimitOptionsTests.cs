using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public sealed class RateLimitOptionsTests
{
    [Fact]
    public void Defaults_are_the_production_limits()
    {
        var options = new RateLimitOptions();

        Assert.Equal(20, options.JoinPerMinute);
        Assert.Equal(40, options.RoomAdminPerMinute);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-5, 40)]
    public void Limits_below_one_are_invalid(int join, int admin) =>
        Assert.False(RateLimitOptions.IsValid(new RateLimitOptions { JoinPerMinute = join, RoomAdminPerMinute = admin }));

    [Fact]
    public async Task Unconfigured_server_allows_exactly_twenty_joins_a_minute()
    {
        using var factory = new LimitedFactory(joinPerMinute: null);

        var statuses = await JoinRepeatedlyAsync(factory, 21);

        Assert.All(statuses.Take(20), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[20]);
    }

    [Fact]
    public async Task Configured_join_limit_is_honoured()
    {
        using var factory = new LimitedFactory(joinPerMinute: 3);

        var statuses = await JoinRepeatedlyAsync(factory, 4);

        Assert.All(statuses.Take(3), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[3]);
    }

    private static async Task<List<HttpStatusCode>> JoinRepeatedlyAsync(WebApplicationFactory<Program> factory, int attempts)
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < attempts; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/join", new JoinRequest("nowhere", "Nobody", "0000"))).StatusCode);
        return statuses;
    }

    private sealed class LimitedFactory(int? joinPerMinute) : WebApplicationFactory<Program>
    {
        private readonly TestDatabase database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
            builder.UseSetting("Room:JwtSigningKey", "test-only-signing-key-0123456789-abcdef");
            if (joinPerMinute is { } limit) builder.UseSetting("RateLimits:JoinPerMinute", limit.ToString());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            database.Dispose();
        }
    }
}
