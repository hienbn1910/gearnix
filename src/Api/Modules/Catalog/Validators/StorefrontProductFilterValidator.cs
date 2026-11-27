using FluentValidation;

using Gearnix.Api.Modules.Catalog.Dtos;

namespace Gearnix.Api.Modules.Catalog.Validators;

public sealed class StorefrontProductFilterValidator : AbstractValidator<StorefrontProductFilter>
{
    public StorefrontProductFilterValidator()
    {
        // No brand is longer than an Admin may type, so a longer one can only be a mistake.
        RuleFor(filter => filter.Brand).MaximumLength(ProductRequestValidator.BrandMaxLength);

        // The same limits as a SKU price, except 0 is allowed as a lower bound: whole VND up to 1 billion.
        RuleFor(filter => filter.MinPrice!.Value).Must(BeAPriceBound).WithMessage(PriceBoundMessage)
            .OverridePropertyName(nameof(StorefrontProductFilter.MinPrice))
            .When(filter => filter.MinPrice is not null);
        RuleFor(filter => filter.MaxPrice!.Value).Must(BeAPriceBound).WithMessage(PriceBoundMessage)
            .OverridePropertyName(nameof(StorefrontProductFilter.MaxPrice))
            .When(filter => filter.MaxPrice is not null);

        // Model binding turns any number into the enum, so "sort=99" arrives as an undefined value.
        RuleFor(filter => filter.Sort).IsInEnum();

        RuleFor(filter => filter.MinPrice)
            .LessThanOrEqualTo(filter => filter.MaxPrice).WithMessage("Minimum price must not be above the maximum price.")
            .When(filter => filter.MinPrice is not null && filter.MaxPrice is not null);
    }

    private const string PriceBoundMessage = "Price must be a whole number of VND from 0 to 1,000,000,000.";

    private static bool BeAPriceBound(decimal price) =>
        price >= 0 && price <= SkuRequestValidator.MaxPrice && price == decimal.Truncate(price);
}
