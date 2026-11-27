namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>A top-level category with its sub-categories, both ordered by sort order, then name.</summary>
public sealed record CategoryTreeItem(Guid Id, string Name, string Slug, IReadOnlyList<CategoryTreeChild> Children);
