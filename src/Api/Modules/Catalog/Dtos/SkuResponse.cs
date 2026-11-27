namespace Gearnix.Api.Modules.Catalog.Dtos;

public sealed record SkuResponse(
    Guid Id,
    Guid ProductId,
    string SkuCode,
    string VariantName,
    decimal Price,
    IReadOnlyDictionary<string, string> Attributes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
