using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RandomRoom.Api.Services;

/// <summary>Turns rule violations into 403/409 problems and hides internals for everything else.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception is RoomRuleException rule
            ? new ProblemDetails
            {
                Status = rule.Violation == RuleViolation.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status409Conflict,
                Title = rule.Message,
            }
            : Unexpected(exception);

        http.Response.StatusCode = problem.Status!.Value;
        await http.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private ProblemDetails Unexpected(Exception exception)
    {
        logger.LogError(exception, "Unhandled exception");
        return new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Something went wrong." };
    }
}
