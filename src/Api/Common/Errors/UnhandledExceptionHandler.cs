using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using Npgsql;

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

        var (status, title) = IsTransientDatabaseFailure(exception)
            ? (StatusCodes.Status503ServiceUnavailable, "The database is temporarily unavailable.")
            : (StatusCodes.Status500InternalServerError, "An unexpected error occurred.");

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }

    /// <summary>
    /// True for failures that may succeed on retry (connection refused, network error, pool exhausted).
    /// EF Core may wrap the Npgsql exception, so the whole inner-exception chain is checked.
    /// Other database errors (e.g. an unhandled unique violation) are programming errors and stay 500.
    /// </summary>
    private static bool IsTransientDatabaseFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true })
            {
                return true;
            }
        }

        return false;
    }
}
