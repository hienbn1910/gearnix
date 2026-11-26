namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Stable error codes sent in every ProblemDetails response as "code" (Workflow.md §9).
/// The frontend shows messages based on these codes, so they never change once published.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string BadRequest = "bad_request";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
    public const string ServiceUnavailable = "service_unavailable";
    public const string DatabaseUnavailable = "database_unavailable";

    // Codes with a fixed meaning beyond their status (Workflow.md §9).
    public const string CsrfHeaderMissing = "csrf_header_missing";

    /// <summary>
    /// The generic code for a status, used when nothing more specific set one.
    /// Unlisted client errors (4xx) → "bad_request"; unlisted server errors (5xx) → "internal_error".
    /// </summary>
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ValidationFailed,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status429TooManyRequests => RateLimited,
        StatusCodes.Status503ServiceUnavailable => ServiceUnavailable,
        >= 400 and < 500 => BadRequest,
        _ => InternalError,
    };
}
