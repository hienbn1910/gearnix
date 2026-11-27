using System.Linq.Expressions;
using System.Text.Json;

using Gearnix.Api.Data;
using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>EF Core queries for products and their SKUs; no business rules here.</summary>
public sealed class ProductRepository(AppDbContext dbContext)
{
    /// <param name="normalizedQuery">Search text already normalized like <c>NameNormalized</c>, or <c>null</c>.</param>
    public async Task<(IReadOnlyList<ProductListItem> Items, int TotalCount)> ListPageAsync(
        string? normalizedQuery, Guid? categoryId, bool? isActive, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking();
        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            // LIKE '%text%' can use the trigram index on name_normalized; the text's own % and _ are escaped with \.
            var pattern = "%" + EscapeLike(normalizedQuery) + "%";
            query = query.Where(p => EF.Functions.Like(p.NameNormalized, pattern, LikeEscape));
        }

        if (categoryId is { } category)
        {
            query = query.Where(p => p.CategoryId == category);
        }

        if (isActive is { } active)
        {
            query = query.Where(p => p.IsActive == active);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id) // newest first; Id makes ties stable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItem(
                p.Id, p.Name, p.Slug, p.Brand, p.CategoryId, p.MinPrice, p.IsActive,
                dbContext.Skus.Count(s => s.ProductId == p.Id)))
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    /// <summary>
    /// A visible product: active, with at least one active SKU. The min price is kept empty exactly when a product
    /// has no active SKU, so it answers the second half without a subquery. The storefront list and product page
    /// both use this one definition.
    /// </summary>
    private static readonly Expression<Func<Product, bool>> IsVisible = p => p.IsActive && p.MinPrice != null;

    public async Task<(IReadOnlyList<StorefrontProductListItem> Items, int TotalCount)> ListVisiblePageAsync(
        StorefrontProductFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking().Where(IsVisible);
        if (filter.CategoryId is { } categoryId)
        {
            // The category itself and its sub-categories; categories are at most two levels deep, so one level of
            // children is the whole subtree. Runs as a subquery in the same SQL statement.
            var categoryIds = dbContext.Categories
                .Where(c => c.Id == categoryId || c.ParentId == categoryId)
                .Select(c => c.Id);
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Brand))
        {
            // Brands are stored trimmed; lower() on both sides makes "kivo" match "Kivo".
            var brand = filter.Brand.Trim().ToLowerInvariant();
            query = query.Where(p => p.Brand.ToLower() == brand);
        }

        // A product is in the price range when its cheapest active SKU is (Workflow.md §10).
        if (filter.MinPrice is { } minPrice)
        {
            query = query.Where(p => p.MinPrice >= minPrice);
        }

        if (filter.MaxPrice is { } maxPrice)
        {
            query = query.Where(p => p.MinPrice <= maxPrice);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Sort(query, filter.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new StorefrontProductListItem(p.Id, p.Slug, p.Name, p.Brand, p.MinPrice!.Value, p.CategoryId))
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    /// <summary>A visible product with its category and active SKUs (cheapest first), in one query.</summary>
    public async Task<StorefrontProduct?> GetVisibleBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var found = await dbContext.Products.AsNoTracking()
            .Where(IsVisible)
            .Where(p => p.Slug == slug)
            .Join(dbContext.Categories, p => p.CategoryId, c => c.Id, (p, c) => new
            {
                Product = p,
                Category = new StorefrontCategory(c.Id, c.Name, c.Slug),
                Skus = dbContext.Skus
                    .Where(s => s.ProductId == p.Id && s.IsActive)
                    .OrderBy(s => s.Price).ThenBy(s => s.Id)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var p = found.Product;
        return new StorefrontProduct(
            p.Id, p.Slug, p.Name, p.Brand, p.Description,
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(p.Specs) ?? [],
            p.MinPrice!.Value,
            found.Category,
            found.Skus.Select(s => new StorefrontSku(
                s.Id, s.SkuCode, s.VariantName, s.Price,
                JsonSerializer.Deserialize<Dictionary<string, string>>(s.Attributes) ?? [])).ToList());
    }

    /// <summary>Id is the last key of every order, so equal prices or times never swap places between pages.</summary>
    private static IOrderedQueryable<Product> Sort(IQueryable<Product> query, ProductSort sort) => sort switch
    {
        ProductSort.PriceAsc => query.OrderBy(p => p.MinPrice).ThenBy(p => p.Id),
        ProductSort.PriceDesc => query.OrderByDescending(p => p.MinPrice).ThenByDescending(p => p.Id),
        _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
    };

    /// <summary>The product with all its SKUs, in one query.</summary>
    public async Task<ProductResponse?> GetResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await dbContext.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Product = p,
                Skus = dbContext.Skus
                    .Where(s => s.ProductId == p.Id)
                    .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        return found is null ? null : ToResponse(found.Product, found.Skus.Select(ToResponse).ToList());
    }

    /// <summary>The product, tracked so that changes to it are saved.</summary>
    public Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>Slugs equal to <paramref name="baseSlug"/> or starting with <c>baseSlug-</c>, in one query.</summary>
    public async Task<IReadOnlyList<string>> GetSlugsLikeAsync(string baseSlug, CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking()
            .Where(p => p.Slug == baseSlug || p.Slug.StartsWith(baseSlug + "-"))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);

    public void Add(Product product) => dbContext.Products.Add(product);

    public void Add(Sku sku) => dbContext.Skus.Add(sku);

    /// <summary>The SKU, tracked, only if it belongs to the product.</summary>
    public Task<Sku?> FindSkuAsync(Guid productId, Guid skuId, CancellationToken cancellationToken) =>
        dbContext.Skus.SingleOrDefaultAsync(s => s.Id == skuId && s.ProductId == productId, cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// Locks the product row until the current transaction ends (<c>SELECT … FOR UPDATE</c>), so SKU changes of one
    /// product run one after another. Returns <c>false</c> if the product does not exist.
    /// </summary>
    public async Task<bool> LockProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var locked = await dbContext.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM products WHERE id = {productId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return locked.Count == 1;
    }

    /// <summary>
    /// Sets the product's min price to the lowest price of its active SKUs (<c>null</c> if none), in one UPDATE
    /// computed by the database. Call it with the product row locked, after the SKU change is saved.
    /// </summary>
    public Task RecomputeMinPriceAsync(Guid productId, CancellationToken cancellationToken) =>
        dbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                p => p.MinPrice,
                p => dbContext.Skus.Where(s => s.ProductId == p.Id && s.IsActive).Min(s => (decimal?)s.Price)),
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>Forgets a failed insert, so nothing else in this request tries to save it again.</summary>
    public void ClearTracked() => dbContext.ChangeTracker.Clear();

    public static ProductResponse ToResponse(Product p, IReadOnlyList<SkuResponse> skus) => new(
        p.Id, p.CategoryId, p.Name, p.Slug, p.Brand, p.Description,
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(p.Specs) ?? [],
        p.MinPrice, p.IsActive, p.CreatedAt, p.UpdatedAt, skus);

    public static SkuResponse ToResponse(Sku s) => new(
        s.Id, s.ProductId, s.SkuCode, s.VariantName, s.Price,
        JsonSerializer.Deserialize<Dictionary<string, string>>(s.Attributes) ?? [],
        s.IsActive, s.CreatedAt, s.UpdatedAt);

    private const string LikeEscape = @"\";

    /// <summary>Makes <c>\</c>, <c>%</c> and <c>_</c> match themselves in a LIKE pattern.</summary>
    private static string EscapeLike(string text) =>
        text.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
}
