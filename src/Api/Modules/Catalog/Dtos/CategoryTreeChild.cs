namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>A sub-category; categories are at most two levels deep, so it has no children.</summary>
public sealed record CategoryTreeChild(Guid Id, string Name, string Slug);
