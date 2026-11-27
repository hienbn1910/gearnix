namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>Create or fully replace a SKU of a product.</summary>
/// <param name="Price">Whole VND amount, above 0 and at most 1,000,000,000.</param>
/// <param name="Attributes">Flat object of strings, e.g. <c>{"storage": "128GB", "color": "Đen"}</c>.</param>
public sealed record SkuRequest(
    string SkuCode,
    string VariantName,
    decimal Price,
    Dictionary<string, string>? Attributes,
    bool IsActive = true);
