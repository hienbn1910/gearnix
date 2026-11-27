using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gearnix.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>Name of the unique index on slugs; a violation of it means two items got the same slug at once.</summary>
    public const string SlugUniqueIndex = "ix_products_slug";

    public void Configure(EntityTypeBuilder<Product> product)
    {
        product.ToTable("products");
        product.Property(p => p.Specs).HasColumnType("jsonb");

        product.HasIndex(p => p.Slug).IsUnique().HasDatabaseName(SlugUniqueIndex);
        product.HasIndex(p => p.MinPrice); // price filter and sort
        product.HasIndex(p => new { p.CreatedAt, p.Id }); // admin list, newest first (read backwards)
        // Trigram index: fast "contains" search on the normalized name (Workflow.md §10).
        product.HasIndex(p => p.NameNormalized).HasMethod("gin").HasOperators("gin_trgm_ops");

        // The storefront brand filter uses an index on lower(brand); EF Core cannot describe an expression index,
        // so it is created in SQL by the AddProductBrandIndex migration.

        // The foreign key also creates the (CategoryId) index from Workflow.md §6.6.
        product.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
