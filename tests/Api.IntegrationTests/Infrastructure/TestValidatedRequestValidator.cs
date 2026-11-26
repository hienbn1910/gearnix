using FluentValidation;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

public sealed class TestValidatedRequestValidator : AbstractValidator<TestValidatedRequest>
{
    public TestValidatedRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty();
        RuleFor(request => request.Quantity).GreaterThan(0);
    }
}
