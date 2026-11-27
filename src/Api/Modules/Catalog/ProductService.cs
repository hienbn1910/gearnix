using System.Text.Json;

using Gearnix.Api.Common.Errors;
using Gearnix.Api.Common.Paging;
using Gearnix.Api.Data.Configurations;
using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Entities;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>Admin management of products and their SKUs (Workflow.md §6.1, §9).</summary>
public sealed class ProductService(
    ProductRepository products,
    CategoryRepository categories,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<ProductListItem>> ListAsync(
        PageRequest page, ProductListFilter filter, CancellationToken cancellationToken)
    {
        // The search text is normalized the same way as the names it is compared with.
        var query = string.IsNullOrWhiteSpace(filter.Q) ? null : VietnameseText.Normalize(filter.Q);
        var (items, totalCount) = await products.ListPageAsync(
            query, filter.CategoryId, filter.IsActive, page.Page, page.PageSize, cancellationToken);
        return new PagedResponse<ProductListItem>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<Result<ProductResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await products.GetResponseAsync(id, cancellationToken) is { } product ? product : CatalogErrors.NotFound;

    public async Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken))
        {
            return CatalogErrors.CategoryNotFound;
        }

        var name = request.Name.Trim();
        var baseSlug = VietnameseText.ToSlug(name);
        var now = CatalogClock.UtcNow(timeProvider);
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            CategoryId = request.CategoryId,
            Name = name,
            NameNormalized = VietnameseText.Normalize(name),
            Slug = CatalogSlugs.PickFree(baseSlug, await products.GetSlugsLikeAsync(baseSlug, cancellationToken)),
            Brand = request.Brand.Trim(),
            Description = request.Description?.Trim() ?? "",
            Specs = SerializeSpecs(request.Specs),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        products.Add(product);

        try
        {
            await products.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, ProductConfiguration.SlugUniqueIndex))
        {
            // Another request with the same name took this slug between our check and our insert.
            products.ClearTracked();
            return CatalogErrors.SlugConflict;
        }

        return ProductRepository.ToResponse(product, []);
    }

    /// <summary>Full replace of the editable fields. The slug never changes; the search name follows the name.</summary>
    public async Task<Result<ProductResponse>> UpdateAsync(
        Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.FindAsync(id, cancellationToken);
        if (product is null)
        {
            return CatalogErrors.NotFound;
        }

        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken))
        {
            return CatalogErrors.CategoryNotFound;
        }

        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.NameNormalized = VietnameseText.Normalize(product.Name);
        product.Brand = request.Brand.Trim();
        product.Description = request.Description?.Trim() ?? "";
        product.Specs = SerializeSpecs(request.Specs);
        product.IsActive = request.IsActive;
        product.UpdatedAt = CatalogClock.UtcNow(timeProvider);
        await products.SaveChangesAsync(cancellationToken);

        // Read back with the SKUs, which this request did not load.
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SkuResponse>> CreateSkuAsync(
        Guid productId, SkuRequest request, CancellationToken cancellationToken)
    {
        var now = CatalogClock.UtcNow(timeProvider);
        var sku = new Sku
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            SkuCode = NormalizeSkuCode(request.SkuCode),
            VariantName = request.VariantName.Trim(),
            Price = request.Price,
            Attributes = SerializeAttributes(request.Attributes),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return await ChangeSkuAsync(productId, cancellationToken, () =>
        {
            products.Add(sku);
            return Task.FromResult<Sku?>(sku);
        });
    }

    /// <summary>Full replace of a SKU's editable fields.</summary>
    public async Task<Result<SkuResponse>> UpdateSkuAsync(
        Guid productId, Guid skuId, SkuRequest request, CancellationToken cancellationToken)
    {
        return await ChangeSkuAsync(productId, cancellationToken, async () =>
        {
            var sku = await products.FindSkuAsync(productId, skuId, cancellationToken);
            if (sku is not null)
            {
                sku.SkuCode = NormalizeSkuCode(request.SkuCode);
                sku.VariantName = request.VariantName.Trim();
                sku.Price = request.Price;
                sku.Attributes = SerializeAttributes(request.Attributes);
                sku.IsActive = request.IsActive;
                sku.UpdatedAt = CatalogClock.UtcNow(timeProvider);
            }

            return sku;
        });
    }

    /// <summary>
    /// One transaction for any SKU change: lock the product row, apply the change, save it, then recompute the
    /// product's min price. The lock makes concurrent SKU changes of the same product wait for each other, so each
    /// recompute sees every price committed before it (Workflow.md §6.1).
    /// </summary>
    /// <param name="applyChange">Adds or edits the SKU; returns <c>null</c> if the SKU does not exist.</param>
    private async Task<Result<SkuResponse>> ChangeSkuAsync(
        Guid productId, CancellationToken cancellationToken, Func<Task<Sku?>> applyChange)
    {
        await using var transaction = await products.BeginTransactionAsync(cancellationToken);
        if (!await products.LockProductAsync(productId, cancellationToken))
        {
            return CatalogErrors.NotFound; // disposing the transaction rolls it back
        }

        var sku = await applyChange();
        if (sku is null)
        {
            return CatalogErrors.NotFound;
        }

        try
        {
            await products.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, SkuConfiguration.SkuCodeUniqueIndex))
        {
            // The unique index is the real guard: it also catches two Admins adding the same code at once.
            // Roll back before anything else, and forget the failed change (Rule.md §3).
            await transaction.RollbackAsync(cancellationToken);
            products.ClearTracked();
            return CatalogErrors.SkuCodeTaken;
        }

        await products.RecomputeMinPriceAsync(productId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductRepository.ToResponse(sku);
    }

    private static string NormalizeSkuCode(string skuCode) => skuCode.Trim().ToUpperInvariant();

    private static string SerializeAttributes(Dictionary<string, string>? attributes) =>
        JsonSerializer.Serialize(attributes ?? []);

    private static string SerializeSpecs(Dictionary<string, JsonElement>? specs) =>
        JsonSerializer.Serialize(specs ?? []);

    private static bool IsUniqueViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgres && postgres.ConstraintName == indexName;
}
