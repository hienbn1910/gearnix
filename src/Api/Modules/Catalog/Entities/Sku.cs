namespace Gearnix.Api.Modules.Catalog.Entities;

/// <summary>A sellable variant of a product; prices and stock belong to SKUs (Workflow.md §6.1).</summary>
public sealed class Sku
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    /// <summary>Unique, upper case, e.g. <c>NOVAX-128-BLK</c>.</summary>
    public required string SkuCode { get; set; }

    public required string VariantName { get; set; }

    /// <summary>Current price in VND; always a whole number above 0.</summary>
    public decimal Price { get; set; }

    /// <summary>What distinguishes this SKU, as a flat JSON object of strings (jsonb).</summary>
    public required string Attributes { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }
}
