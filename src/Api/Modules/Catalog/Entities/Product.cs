namespace Gearnix.Api.Modules.Catalog.Entities;

/// <summary>A sellable item as customers see it; its SKUs carry prices and stock (Workflow.md §6.1).</summary>
public sealed class Product
{
    public Guid Id { get; init; }

    public Guid CategoryId { get; set; }

    public required string Name { get; set; }

    /// <summary>The name lowercased without diacritics, for search; recomputed whenever the name changes.</summary>
    public required string NameNormalized { get; set; }

    /// <summary>Generated from the name when the product is created; never changes, so links keep working.</summary>
    public required string Slug { get; init; }

    public required string Brand { get; set; }

    public required string Description { get; set; }

    /// <summary>Technical details as a flat JSON object (jsonb), e.g. <c>{"cpu": "N1", "ramGb": 16}</c>.</summary>
    public required string Specs { get; set; }

    /// <summary>
    /// Lowest price among the product's active SKUs; <c>null</c> when it has none. Recomputed by the database in the
    /// same transaction as every SKU change, never set from C#.
    /// </summary>
    public decimal? MinPrice { get; init; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }
}
