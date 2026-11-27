using System.Diagnostics;

namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Makes every ProblemDetails response carry "code" and "traceId" (Workflow.md §9, §11.4),
/// and every validation error map use camelCase field names.
/// </summary>
public static class ProblemDetailsSetup
{
    public static IServiceCollection AddGearnixProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;

            // A specific code (e.g. "out_of_stock") set earlier is kept; otherwise use the generic one.
            problem.Extensions.TryAdd("code", ErrorCodes.ForStatus(status));
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            if (problem is HttpValidationProblemDetails validationProblem)
            {
                ValidationErrorKeys.RewriteKeysToCamelCase(validationProblem.Errors);
            }
        });
}
