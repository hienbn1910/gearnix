namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>An active SKU on the storefront product page; no stock here (it has its own endpoint).</summary>
public sealed record StorefrontSku(
    Guid Id, string SkuCode, string VariantName, decimal Price, IReadOnlyDictionary<string, string> Attributes);
