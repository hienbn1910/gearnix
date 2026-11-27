using Gearnix.Api.Common.Errors;
using Gearnix.Api.Common.Paging;
using Gearnix.Api.Data.Configurations;
using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>Admin management of categories (Workflow.md §6.1, §9).</summary>
public sealed class CategoryService(CategoryRepository categories, TimeProvider timeProvider)
{
    public async Task<PagedResponse<CategoryResponse>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await categories.ListPageAsync(page.Page, page.PageSize, cancellationToken);
        return new PagedResponse<CategoryResponse>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<CategoryResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await categories.GetResponseAsync(id, cancellationToken) is { } category ? category : CatalogErrors.NotFound;

    public async Task<Result<CategoryResponse>> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var baseSlug = VietnameseText.ToSlug(name);
        var slug = CatalogSlugs.PickFree(baseSlug, await categories.GetSlugsLikeAsync(baseSlug, cancellationToken));

        await using var transaction = await categories.BeginTransactionAsync(cancellationToken);
        if (request.ParentId is { } parentId)
        {
            // Locked so that nobody gives the parent a parent of its own before this child is saved.
            await categories.LockAsync([parentId], cancellationToken);
            if (await CheckParentAsync(parentId, cancellationToken) is { } parentError)
            {
                return parentError; // disposing the transaction rolls it back
            }
        }

        var now = CatalogClock.UtcNow(timeProvider);
        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Slug = slug,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        categories.Add(category);

        try
        {
            await categories.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (DatabaseFailures.IsUniqueViolation(exception, CategoryConfiguration.SlugUniqueIndex))
        {
            // Another request with the same name took this slug between our check and our insert.
            await transaction.RollbackAsync(cancellationToken);
            categories.ClearTracked();
            return CatalogErrors.SlugConflict;
        }

        await transaction.CommitAsync(cancellationToken);
        return CategoryRepository.ToResponse(category);
    }

    /// <summary>Full replace of name, parent and sort order. The slug never changes.</summary>
    public async Task<Result<CategoryResponse>> UpdateAsync(
        Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await categories.BeginTransactionAsync(cancellationToken);

        // The two-level rule reads other rows (the parent, the children), so the rows involved are locked first:
        // two Admins moving categories at the same moment are checked one after the other, never both against
        // the old tree. A new child takes the same lock on its parent (CreateAsync).
        await categories.LockAsync(request.ParentId is { } newParent ? [id, newParent] : [id], cancellationToken);

        var category = await categories.FindAsync(id, cancellationToken);
        if (category is null)
        {
            return CatalogErrors.NotFound;
        }

        if (request.ParentId is { } parentId)
        {
            // A category with children is already a parent; giving it a parent would make a third level.
            if (parentId == id || await categories.HasChildrenAsync(id, cancellationToken))
            {
                return CatalogErrors.InvalidCategoryParent;
            }

            if (await CheckParentAsync(parentId, cancellationToken) is { } parentError)
            {
                return parentError;
            }
        }

        category.Name = request.Name.Trim();
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = CatalogClock.UtcNow(timeProvider);
        await categories.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CategoryRepository.ToResponse(category);
    }

    /// <summary>The parent must exist and be top-level, so the tree is never deeper than two levels.</summary>
    private async Task<Error?> CheckParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var (exists, grandparentId) = await categories.GetParentIdAsync(parentId, cancellationToken);
        return !exists || grandparentId is not null ? CatalogErrors.InvalidCategoryParent : null;
    }
}
