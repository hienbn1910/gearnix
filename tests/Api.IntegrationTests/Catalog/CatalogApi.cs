using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>Shortcuts for building catalog data through the admin API, the way an Admin would.</summary>
internal static class CatalogApi
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A name no other test uses, because all tests share one database.</summary>
    public static string UniqueName(string prefix) => $"{prefix} {Guid.CreateVersion7():N}";

    public static async Task<CategoryBody> CreateCategoryAsync(
        HttpClient admin, string? name = null, Guid? parentId = null, int sortOrder = 0)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/categories",
            new { name = name ?? UniqueName("Danh mục"), parentId, sortOrder }, Token);
        return await ReadCreatedAsync<CategoryBody>(response);
    }

    public static async Task<ProductBody> CreateProductAsync(
        HttpClient admin, Guid categoryId, string? name = null, bool isActive = true)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/products",
            new
            {
                categoryId,
                name = name ?? UniqueName("Sản phẩm"),
                brand = "Novatech",
                description = "Mô tả",
                specs = new { cpu = "N1", ramGb = 8 },
                isActive,
            }, Token);
        return await ReadCreatedAsync<ProductBody>(response);
    }

    public static async Task<SkuBody> CreateSkuAsync(
        HttpClient admin, Guid productId, decimal price, bool isActive = true, string? skuCode = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/admin/products/{productId}/skus",
            SkuRequest(skuCode ?? UniqueSkuCode(), price, isActive), Token);
        return await ReadCreatedAsync<SkuBody>(response);
    }

    public static object SkuRequest(string skuCode, decimal price, bool isActive = true) => new
    {
        skuCode,
        variantName = "128GB - Đen",
        price,
        attributes = new { storage = "128GB", color = "Đen" },
        isActive,
    };

    public static string UniqueSkuCode() => $"SKU-{Guid.CreateVersion7():N}"[..30].ToUpperInvariant();

    public static async Task<ProductBody> GetProductAsync(HttpClient admin, Guid productId) =>
        await admin.GetFromJsonAsync<ProductBody>($"/api/admin/products/{productId}", Token)
        ?? throw new InvalidOperationException("Empty product body.");

    private static async Task<T> ReadCreatedAsync<T>(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"Expected 201, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Token)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(Token)
            ?? throw new InvalidOperationException("Empty response body.");
    }
}

internal sealed record CategoryBody(
    Guid Id, string Name, string Slug, Guid? ParentId, int SortOrder, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record ProductBody(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Slug,
    string Brand,
    string Description,
    Dictionary<string, JsonElement> Specs,
    decimal? MinPrice,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<SkuBody> Skus);

internal sealed record ProductListItemBody(
    Guid Id, string Name, string Slug, string Brand, Guid CategoryId, decimal? MinPrice, bool IsActive, int SkuCount);

internal sealed record SkuBody(
    Guid Id,
    Guid ProductId,
    string SkuCode,
    string VariantName,
    decimal Price,
    Dictionary<string, string> Attributes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record PageBody<T>(List<T> Items, int Page, int PageSize, int TotalCount);
