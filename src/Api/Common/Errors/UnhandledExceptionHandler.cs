using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Turns any exception nobody caught into a ProblemDetails response:
/// a temporary database failure → 503 (Workflow.md §11.3), anything else → 500.
/// The response never contains the exception message, stack trace or SQL (Rule.md §5); the log does.
/// </summary>
public sealed class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (status, code, title) = DatabaseFailures.IsUnavailable(exception)
            ? (StatusCodes.Status503ServiceUnavailable, ErrorCodes.DatabaseUnavailable, "The database is temporarily unavailable.")
            : (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.");

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code },
            },
        });
    }
}
