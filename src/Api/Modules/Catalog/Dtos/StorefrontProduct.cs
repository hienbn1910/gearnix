using System.Text.Json;

namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>The storefront product page: a visible product with its active SKUs, cheapest first.</summary>
public sealed record StorefrontProduct(
    Guid Id,
    string Slug,
    string Name,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, JsonElement> Specs,
    decimal MinPrice,
    StorefrontCategory Category,
    IReadOnlyList<StorefrontSku> Skus);
