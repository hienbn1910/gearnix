using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Gearnix.Api.Common.Validation;

/// <summary>
/// Runs the FluentValidation validator of every action argument before the action runs (Rule.md §5).
/// Any failure → 400 ValidationProblemDetails; the action is not called.
/// Field names become camelCase when the response is written (<see cref="Errors.ValidationErrorKeys"/>).
/// Arguments without a registered validator are skipped.
/// </summary>
public sealed class FluentValidationFilter(ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Validators are scoped and depend on the argument's runtime type, so they are resolved per request.
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            var problem = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        await next();
    }
}
