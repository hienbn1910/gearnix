using FluentValidation;

using Gearnix.Api.Modules.Identity.Dtos;
using Gearnix.Api.Modules.Identity.Entities;

namespace Gearnix.Api.Modules.Identity.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int EmailMaxLength = 256;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public RegisterRequestValidator()
    {
        // Report only the first failing rule of each field.
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Strings are trimmed before validation (the service trims the same way before saving).
        RuleFor(request => Trim(request.Email))
            .NotEmpty().MaximumLength(EmailMaxLength).EmailAddress()
            .OverridePropertyName(nameof(RegisterRequest.Email));

        // Length over complexity (NIST SP 800-63B); the password itself is never trimmed.
        RuleFor(request => request.Password).Length(PasswordMinLength, PasswordMaxLength);

        RuleFor(request => Trim(request.FullName))
            .NotEmpty().MaximumLength(AppUser.FullNameMaxLength)
            .OverridePropertyName(nameof(RegisterRequest.FullName));

        // Vietnamese phone number: 0 followed by 9 digits.
        RuleFor(request => Trim(request.Phone))
            .Matches(@"^0\d{9}$")
            .OverridePropertyName(nameof(RegisterRequest.Phone))
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => Trim(request.City))
            .MaximumLength(AppUser.CityMaxLength)
            .OverridePropertyName(nameof(RegisterRequest.City));
    }

    internal static string Trim(string? value) => value?.Trim() ?? "";
}
