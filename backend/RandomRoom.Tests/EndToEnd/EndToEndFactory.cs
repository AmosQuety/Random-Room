using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// A server with its own throwaway database for a test class that signs many players in. The rate limits are
/// raised because every test client shares one address; production keeps the defaults.
/// </summary>
public sealed class EndToEndFactory : WebApplicationFactory<Program>
{
    private const string HighLimit = "100000";

    private readonly TestDatabase database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
        builder.UseSetting("Room:JwtSigningKey", "test-only-signing-key-0123456789-abcdef");
        builder.UseSetting("RateLimits:JoinPerMinute", HighLimit);
        builder.UseSetting("RateLimits:RoomAdminPerMinute", HighLimit);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        database.Dispose();
    }
}
