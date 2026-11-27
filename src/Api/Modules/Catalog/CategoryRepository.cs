using Gearnix.Api.Data;
using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>EF Core queries for categories; no business rules here.</summary>
public sealed class CategoryRepository(AppDbContext dbContext)
{
    public async Task<(IReadOnlyList<CategoryResponse> Items, int TotalCount)> ListPageAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Categories.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id) // Id makes paging stable on ties
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => ToResponse(c))
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<CategoryResponse?> GetResponseAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => ToResponse(c))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>The category, tracked so that changes to it are saved.</summary>
    public Task<Category?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>The parent id of a category: <c>null</c> = no such category; inner <c>null</c> = top-level.</summary>
    public async Task<(bool Exists, Guid? ParentId)> GetParentIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await dbContext.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.ParentId })
            .SingleOrDefaultAsync(cancellationToken);
        return found is null ? (false, null) : (true, found.ParentId);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// Locks the given category rows until the current transaction ends (<c>SELECT … FOR UPDATE</c>), always in id
    /// order so that two requests locking the same pair cannot deadlock. Unknown ids are skipped.
    /// </summary>
    public async Task LockAsync(Guid[] ids, CancellationToken cancellationToken) =>
        await dbContext.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM categories WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Categories.AnyAsync(c => c.Id == id, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Categories.AnyAsync(c => c.ParentId == id, cancellationToken);

    /// <summary>Slugs equal to <paramref name="baseSlug"/> or starting with <c>baseSlug-</c>, in one query.</summary>
    public async Task<IReadOnlyList<string>> GetSlugsLikeAsync(string baseSlug, CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .Where(c => c.Slug == baseSlug || c.Slug.StartsWith(baseSlug + "-"))
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);

    public void Add(Category category) => dbContext.Categories.Add(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>Forgets a failed insert, so nothing else in this request tries to save it again.</summary>
    public void ClearTracked() => dbContext.ChangeTracker.Clear();

    public static CategoryResponse ToResponse(Category c) =>
        new(c.Id, c.Name, c.Slug, c.ParentId, c.SortOrder, c.CreatedAt, c.UpdatedAt);
}
