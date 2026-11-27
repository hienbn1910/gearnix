namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>The whole category tree for the storefront menu; not paginated (Workflow.md §9).</summary>
public sealed record CategoryTree(IReadOnlyList<CategoryTreeItem> Items);
