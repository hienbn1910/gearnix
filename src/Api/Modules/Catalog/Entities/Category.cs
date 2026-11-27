namespace Gearnix.Api.Modules.Catalog.Entities;

/// <summary>A group of products, at most two levels deep (Workflow.md §6.1).</summary>
public sealed class Category
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    /// <summary>Generated from the name when the category is created; never changes, so links keep working.</summary>
    public required string Slug { get; init; }

    /// <summary>The top-level category this one sits under; <c>null</c> for a top-level category.</summary>
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }
}
