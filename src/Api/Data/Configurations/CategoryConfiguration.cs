using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gearnix.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <summary>Name of the unique index on slugs; a violation of it means two items got the same slug at once.</summary>
    public const string SlugUniqueIndex = "ix_categories_slug";

    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("categories");
        category.HasIndex(c => c.Slug).IsUnique().HasDatabaseName(SlugUniqueIndex);

        // A parent cannot be removed while it has children (and categories are never deleted anyway).
        category.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
