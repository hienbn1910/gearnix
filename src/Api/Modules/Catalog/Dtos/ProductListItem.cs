namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>One row of the admin product list.</summary>
public sealed record ProductListItem(
    Guid Id,
    string Name,
    string Slug,
    string Brand,
    Guid CategoryId,
    decimal? MinPrice,
    bool IsActive,
    int SkuCount);
