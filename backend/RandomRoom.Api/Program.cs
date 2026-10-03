using RandomRoom.Api;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Auth;
using RandomRoom.Api.Data;
using RandomRoom.Api.Endpoints;
using RandomRoom.Api.Games;
using RandomRoom.Api.Games.Shared;
using RandomRoom.Api.Games.ForbiddenWords;
using RandomRoom.Api.Games.GuessWho;
using RandomRoom.Api.Games.Bingo;
using RandomRoom.Api.Games.Buzzer;
using RandomRoom.Api.Games.SketchGuess;
using RandomRoom.Api.Games.SpinWheel;
using RandomRoom.Api.Games.StoryChain;
using RandomRoom.Api.Games.WordSpies;
using RandomRoom.Api.Games.RandomPicker;
using RandomRoom.Api.Games.Rounds;
using RandomRoom.Api.Games.TwoTruths;
using RandomRoom.Api.Games.Trivia;
using RandomRoom.Api.Hubs;
using RandomRoom.Api.Services;

DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RoomOptions>()
    .BindConfiguration(RoomOptions.SectionName)
    .Validate(RoomOptions.IsValid, "Room settings need a 32+ char JwtSigningKey.")
    .ValidateOnStart();

builder.Services.AddDbContext<RoomDbContext>(o =>
    o.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration.GetConnectionString("Default"))));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<IRandomChoiceSource, CryptoRandomChoiceSource>();
builder.Services.AddSingleton<IRoomNotifier, SignalRRoomNotifier>();
builder.Services.AddScoped<PlayerTokenService>();
builder.Services.AddSingleton(RoomSnapshotSequencer.Shared);
builder.Services.AddScoped<GameSessionService>();
builder.Services.AddScoped<RoomBroadcaster>();
builder.Services.AddScoped<GameStore>();
builder.Services.AddScoped<RoomAdminService>();
builder.Services.AddScoped<IGameEngine, RandomPickerEngine>();
builder.Services.AddScoped<IGameEngine, TriviaEngine>();
builder.Services.AddScoped<IGameEngine, TwoTruthsEngine>();
builder.Services.AddScoped<IGameEngine, GuessWhoEngine>();
builder.Services.AddScoped<IGameEngine, SpinWheelEngine>();
builder.Services.AddScoped<IGameEngine, BuzzerEngine>();
builder.Services.AddScoped<IGameEngine, BingoEngine>();
builder.Services.AddScoped<IGameEngine, WordSpiesEngine>();
builder.Services.AddScoped<IGameEngine, ForbiddenWordsEngine>();
builder.Services.AddScoped<IGameEngine, SketchGuessEngine>();
builder.Services.AddScoped<IGameEngine>(sp => new StoryChainEngine(new OneWordRules(), sp.GetRequiredService<GameStore>(), sp.GetRequiredService<IRandomChoiceSource>()));
builder.Services.AddScoped<IGameEngine>(sp => new StoryChainEngine(new FortunatelyRules(), sp.GetRequiredService<GameStore>(), sp.GetRequiredService<IRandomChoiceSource>()));
builder.Services.AddRoundGame<IntroPrompt, NameThatRules>();
builder.Services.AddRoundGame<MadLibPrompt, MadLibsRules>();
builder.Services.AddRoundGame<TwoWayPrompt, WouldYouRatherRules>();
builder.Services.AddRoundGame<TwoWayPrompt, ThisOrThatRules>();
builder.Services.AddRoundGame<StatementPrompt, MostLikelyToRules>();
builder.Services.AddRoundGame<StatementPrompt, NeverHaveIEverRules>();
builder.Services.AddRoundGame<SurveyPrompt, SurveyShowdownRules>();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

// Brute-forcing a short PIN is the main attack on identity, so joining is throttled per client address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Tell clients how long to wait and use the same problem+json shape as every other error.
    o.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        await response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many attempts. Please wait a moment and try again." }, ct);
    };
    o.AddPolicy(RoomEndpoints.JoinRateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    // Looser than join: creating rooms and claiming invites aren't credential-guessing targets,
    // but several players behind one home/office IP can legitimately set up a room in quick succession.
    o.AddPolicy(RoomEndpoints.RoomAdminRateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<RoomOptions>>((jwt, room) =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new()
        {
            IssuerSigningKey = PlayerTokenService.SigningKey(room.Value),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // Browsers cannot set headers on WebSocket upgrades, so SignalR sends the token in the query string.
        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/hubs")) ctx.Token = ctx.Request.Query["access_token"];
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<RoomDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; connect-src 'self' ws: wss:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRoomEndpoints();
app.MapHub<RoomHub>("/hubs/room");

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("/room/{**slug}", "index.html");

app.Run();

public partial class Program;
