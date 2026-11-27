using System.Text.Json;

namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>A product with everything an Admin edits, including all its SKUs.</summary>
public sealed record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Slug,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, JsonElement> Specs,
    decimal? MinPrice,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SkuResponse> Skus);
