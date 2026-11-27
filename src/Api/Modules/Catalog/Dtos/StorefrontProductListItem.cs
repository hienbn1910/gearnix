namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>One visible product in the storefront list. A visible product always has a min price.</summary>
public sealed record StorefrontProductListItem(
    Guid Id, string Slug, string Name, string Brand, decimal MinPrice, Guid CategoryId);
