namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>Filters of the storefront product list, bound from the query string (Workflow.md §9, §10).</summary>
public sealed class StorefrontProductFilter
{
    /// <summary>A category; a top-level category also matches its sub-categories.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>One brand, compared without case and surrounding spaces.</summary>
    public string? Brand { get; init; }

    /// <summary>Lowest accepted min price, inclusive.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Highest accepted min price, inclusive.</summary>
    public decimal? MaxPrice { get; init; }

    /// <summary><c>newest</c> (default), <c>priceAsc</c> or <c>priceDesc</c>; see <see cref="ProductSortNames"/>.</summary>
    public string? Sort { get; init; }
}
