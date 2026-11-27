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
        HttpClient admin, Guid categoryId, string? name = null, bool isActive = true, string brand = "Novatech")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/products",
            new
            {
                categoryId,
                name = name ?? UniqueName("Sản phẩm"),
                brand,
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

    /// <summary>The response body as raw JSON, to check exactly which fields a public response exposes.</summary>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Token)}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(Token);
    }

    public static IEnumerable<string> PropertyNames(JsonElement json) =>
        json.EnumerateObject().Select(property => property.Name);

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

internal sealed record CategoryTreeBody(List<CategoryTreeItemBody> Items);

internal sealed record CategoryTreeItemBody(Guid Id, string Name, string Slug, List<CategoryTreeChildBody> Children);

internal sealed record CategoryTreeChildBody(Guid Id, string Name, string Slug);

internal sealed record StorefrontProductBody(Guid Id, string Slug, string Name, string Brand, decimal MinPrice, Guid CategoryId);

internal sealed record StorefrontProductPageBody(
    Guid Id,
    string Slug,
    string Name,
    string Brand,
    string Description,
    Dictionary<string, JsonElement> Specs,
    decimal MinPrice,
    StorefrontCategoryBody Category,
    List<StorefrontSkuBody> Skus);

internal sealed record StorefrontCategoryBody(Guid Id, string Name, string Slug);

internal sealed record StorefrontSkuBody(
    Guid Id, string SkuCode, string VariantName, decimal Price, Dictionary<string, string> Attributes);
