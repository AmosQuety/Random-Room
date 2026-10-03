using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RandomRoom.Api.Endpoints;

namespace RandomRoom.Tests;

/// <summary>Its own fixture instance, so the 20-per-minute join counter is not shared with the other API tests.</summary>
public sealed class RateLimitTests(ApiTests.Factory factory) : IClassFixture<ApiTests.Factory>
{
    [Fact]
    public async Task Joining_too_often_is_refused_with_a_reason_and_when_to_retry()
    {
        var client = factory.CreateClient();
        HttpResponseMessage? refused = null;

        for (var attempt = 0; attempt < 21 && refused is null; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/join", new JoinRequest("nowhere", "Nobody", "0000"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests) refused = response;
        }

        Assert.NotNull(refused);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Retry-After should say how long to wait");
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Contains("Too many", body.GetProperty("title").GetString());
    }
}
