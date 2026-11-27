using FluentValidation;

using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Entities;

namespace Gearnix.Api.Modules.Catalog.Validators;

public sealed class SkuRequestValidator : AbstractValidator<SkuRequest>
{
    public const int SkuCodeMinLength = 3;
    public const int SkuCodeMaxLength = 50;
    public const int VariantNameMaxLength = 100;
    public const decimal MaxPrice = 1_000_000_000m;

    public SkuRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Upper case letters and digits, hyphens only between them, e.g. NOVAX-128-BLK. Compared after upper-casing,
        // the same way the service stores it.
        RuleFor(request => request.SkuCode == null ? null : Sku.NormalizeCode(request.SkuCode))
            .NotEmpty()
            .Length(SkuCodeMinLength, SkuCodeMaxLength)
            .Matches("^[A-Z0-9]+(-[A-Z0-9]+)*$")
            .WithMessage("SKU code may contain only letters, digits and single hyphens between them.")
            .OverridePropertyName(nameof(SkuRequest.SkuCode));

        RuleFor(request => Trim(request.VariantName))
            .NotEmpty().MaximumLength(VariantNameMaxLength)
            .OverridePropertyName(nameof(SkuRequest.VariantName));

        // Whole VND only: no currency in this shop has a smaller unit in use.
        RuleFor(request => request.Price)
            .GreaterThan(0).LessThanOrEqualTo(MaxPrice)
            .Must(price => price == decimal.Truncate(price)).WithMessage("Price must be a whole number of VND.");

        RuleFor(request => request.Attributes)
            .Must(CatalogJsonRules.AreValidAttributes).WithMessage(CatalogJsonRules.AttributesMessage);
    }

    // The JSON body may omit a field even though the record declares it non-nullable.
    private static string? Trim(string? value) => value?.Trim();
}
