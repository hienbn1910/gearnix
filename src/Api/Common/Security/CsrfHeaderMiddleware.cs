using Gearnix.Api.Common.Errors;

using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Common.Security;

/// <summary>
/// Requires "X-CSRF: 1" on every state-changing API request (Workflow.md §11.5, ADR 0006).
/// Browsers cannot add a custom header to a cross-site request without a CORS preflight, which the API does not
/// allow, so another website cannot make a signed-in user's browser call these endpoints. Register and login are
/// covered too (login CSRF). The payment webhook is exempt because it is signed.
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
{
    public const string HeaderName = "X-CSRF";
    public const string HeaderValue = "1";

    private static readonly PathString ApiPath = "/api";
    private static readonly PathString PaymentWebhookPath = "/api/payments/webhook";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresCsrfHeader(context.Request) && context.Request.Headers[HeaderName] != HeaderValue)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Missing CSRF header.",
                    Extensions = { ["code"] = ErrorCodes.CsrfHeaderMissing },
                },
            });
            return;
        }

        await next(context);
    }

    private static bool RequiresCsrfHeader(HttpRequest request) =>
        !IsSafe(request.Method)
        && request.Path.StartsWithSegments(ApiPath)
        && !request.Path.Equals(PaymentWebhookPath, StringComparison.OrdinalIgnoreCase); // exactly this path only

    // An allowlist of read-only methods: any other method, including unusual ones, needs the header.
    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
