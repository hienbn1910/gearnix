namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>The category a storefront product belongs to.</summary>
public sealed record StorefrontCategory(Guid Id, string Name, string Slug);
