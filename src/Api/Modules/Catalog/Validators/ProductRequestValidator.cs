using FluentValidation;

using Gearnix.Api.Modules.Catalog.Dtos;

namespace Gearnix.Api.Modules.Catalog.Validators;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public const int NameMaxLength = 200;
    public const int BrandMaxLength = 100;
    public const int DescriptionMaxLength = 5000;

    public ProductRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.CategoryId).NotEmpty();

        // Strings are trimmed before validation (the service trims the same way before saving).
        RuleFor(request => Trim(request.Name))
            .NotEmpty().MaximumLength(NameMaxLength)
            .OverridePropertyName(nameof(ProductRequest.Name));

        RuleFor(request => Trim(request.Brand))
            .NotEmpty().MaximumLength(BrandMaxLength)
            .OverridePropertyName(nameof(ProductRequest.Brand));

        RuleFor(request => Trim(request.Description))
            .MaximumLength(DescriptionMaxLength)
            .OverridePropertyName(nameof(ProductRequest.Description));

        RuleFor(request => request.Specs)
            .Must(CatalogJsonRules.AreValidSpecs).WithMessage(CatalogJsonRules.SpecsMessage);
    }

    // The JSON body may omit a field even though the record declares it non-nullable.
    private static string? Trim(string? value) => value?.Trim();
}
