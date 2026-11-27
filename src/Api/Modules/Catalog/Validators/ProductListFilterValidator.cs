using FluentValidation;

using Gearnix.Api.Modules.Catalog.Dtos;

namespace Gearnix.Api.Modules.Catalog.Validators;

public sealed class ProductListFilterValidator : AbstractValidator<ProductListFilter>
{
    public ProductListFilterValidator()
    {
        RuleFor(filter => filter.Q).MaximumLength(ProductRequestValidator.NameMaxLength);
    }
}
