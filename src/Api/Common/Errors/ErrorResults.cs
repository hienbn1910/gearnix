using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Turns a business <see cref="Error"/> into a ProblemDetails response, the same way in every controller.
/// </summary>
public static class ErrorResults
{
    public static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown error kind."),
    };

    /// <summary>
    /// A ProblemDetails response with the status of the error's kind and the error's own code.
    /// </summary>
    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = StatusCodeFor(error.Kind);
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, statusCode: status);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
