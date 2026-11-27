using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gearnix.Api.Data.Configurations;

public sealed class SkuConfiguration : IEntityTypeConfiguration<Sku>
{
    /// <summary>Name of the unique index on SKU codes; a violation of it means "SKU code taken".</summary>
    public const string SkuCodeUniqueIndex = "ix_skus_sku_code";

    public void Configure(EntityTypeBuilder<Sku> sku)
    {
        sku.ToTable("skus");
        sku.Property(s => s.Attributes).HasColumnType("jsonb");

        sku.HasIndex(s => s.SkuCode).IsUnique().HasDatabaseName(SkuCodeUniqueIndex);

        // The foreign key also creates the (ProductId) index from Workflow.md §6.6.
        sku.HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
