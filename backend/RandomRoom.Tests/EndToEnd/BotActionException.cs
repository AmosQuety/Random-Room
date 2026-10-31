using System.Net;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>An action the server refused, with what the player was told.</summary>
public sealed class BotActionException(string player, string action, HttpStatusCode status, string body)
    : Exception($"{player}'s '{action}' was refused with {(int)status}: {body}")
{
    public HttpStatusCode Status { get; } = status;
}
