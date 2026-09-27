using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RandomRoom.Api.Services;

/// <summary>Turns rule violations into 403/409 problems and hides internals for everything else.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception is RoomRuleException rule
            ? new ProblemDetails { Status = StatusFor(rule.Violation), Title = rule.Message }
            : Unexpected(exception);

        http.Response.StatusCode = problem.Status!.Value;
        await http.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static int StatusFor(RuleViolation violation) => violation switch
    {
        RuleViolation.Forbidden => StatusCodes.Status403Forbidden,
        RuleViolation.Conflict => StatusCodes.Status409Conflict,
        RuleViolation.InvalidInput => StatusCodes.Status400BadRequest,
        RuleViolation.NotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status500InternalServerError,
    };

    private ProblemDetails Unexpected(Exception exception)
    {
        logger.LogError(exception, "Unhandled exception");
        return new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Something went wrong." };
    }
}
