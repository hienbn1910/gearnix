using FluentValidation;

using Gearnix.Api.Modules.Catalog.Dtos;

namespace Gearnix.Api.Modules.Catalog.Validators;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public const int NameMaxLength = 100;
    public const int SortOrderMax = 1000;

    public CategoryRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Strings are trimmed before validation (the service trims the same way before saving).
        RuleFor(request => Trim(request.Name))
            .NotEmpty().MaximumLength(NameMaxLength)
            .OverridePropertyName(nameof(CategoryRequest.Name));

        RuleFor(request => request.SortOrder).InclusiveBetween(0, SortOrderMax);
    }

    // The JSON body may omit a field even though the record declares it non-nullable.
    private static string? Trim(string? value) => value?.Trim();
}
