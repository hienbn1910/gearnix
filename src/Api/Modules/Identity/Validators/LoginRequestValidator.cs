using FluentValidation;

using Gearnix.Api.Modules.Identity.Dtos;

namespace Gearnix.Api.Modules.Identity.Validators;

/// <summary>
/// Only checks that both fields are present and not absurdly long. Password rules are not repeated here,
/// so a sign-in attempt never reveals the password policy or anything about the account.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => RegisterRequestValidator.Trim(request.Email))
            .NotEmpty().MaximumLength(RegisterRequestValidator.EmailMaxLength)
            .OverridePropertyName(nameof(LoginRequest.Email));
        RuleFor(request => request.Password).NotEmpty().MaximumLength(RegisterRequestValidator.PasswordMaxLength);
    }
}
