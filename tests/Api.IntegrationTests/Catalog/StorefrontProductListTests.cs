using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>
/// The public product list. Every test filters by its own new category (or brand), because all tests share one
/// database; the products are created through the admin API.
/// </summary>
public sealed class StorefrontProductListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task List_Anonymous_OnlyVisibleProductsNewestFirst()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var older = await CreateVisibleProductAsync(admin, category.Id, 9_000_000m);
        var inactive = await CreateProductAsync(admin, category.Id, isActive: false);
        await CreateSkuAsync(admin, inactive.Id, 5_000_000m);
        var noActiveSku = await CreateProductAsync(admin, category.Id);
        await CreateSkuAsync(admin, noActiveSku.Id, 5_000_000m, isActive: false);
        await CreateProductAsync(admin, category.Id); // no SKU at all
        var newer = await CreateVisibleProductAsync(admin, category.Id, 7_000_000m);

        var page = await ListAsync(visitor, $"categoryId={category.Id}");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(item => item.Id));
        Assert.Equal(
            new StorefrontProductBody(newer.Id, newer.Slug, newer.Name, "Novatech", 7_000_000m, category.Id),
            page.Items[0]);
    }

    [Fact]
    public async Task List_SecondPage_ReturnsRemainingItems()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var oldest = await CreateVisibleProductAsync(admin, category.Id, 1_000_000m);
        await CreateVisibleProductAsync(admin, category.Id, 1_000_000m);
        await CreateVisibleProductAsync(admin, category.Id, 1_000_000m);

        var page = await ListAsync(visitor, $"categoryId={category.Id}&page=2&pageSize=2");

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(oldest.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task List_ItemHasOnlyPublicFields()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        await CreateVisibleProductAsync(admin, category.Id, 1_000_000m);

        var json = await ReadJsonAsync(await visitor.GetAsync($"/api/products?categoryId={category.Id}", Token));

        Assert.Equal(["items", "page", "pageSize", "totalCount"], PropertyNames(json).Order());
        Assert.Equal(
            ["brand", "categoryId", "id", "minPrice", "name", "slug"],
            PropertyNames(json.GetProperty("items")[0]).Order());
    }

    [Fact]
    public async Task List_ByTopLevelCategory_IncludesItsSubCategoriesOnly()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var laptops = await CreateCategoryAsync(admin);
        var gaming = await CreateCategoryAsync(admin, parentId: laptops.Id);
        var office = await CreateCategoryAsync(admin, parentId: laptops.Id);
        var inParent = await CreateVisibleProductAsync(admin, laptops.Id, 1_000_000m);
        var inGaming = await CreateVisibleProductAsync(admin, gaming.Id, 1_000_000m);
        var inOffice = await CreateVisibleProductAsync(admin, office.Id, 1_000_000m);

        var byParent = await ListAsync(visitor, $"categoryId={laptops.Id}");
        var bySubCategory = await ListAsync(visitor, $"categoryId={gaming.Id}");

        Assert.Equal([inOffice.Id, inGaming.Id, inParent.Id], byParent.Items.Select(item => item.Id));
        Assert.Equal(inGaming.Id, Assert.Single(bySubCategory.Items).Id);
    }

    [Fact]
    public async Task List_UnknownCategory_ReturnsEmptyPage()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var visitor = factory.CreateApiClient();

        var page = await ListAsync(visitor, $"categoryId={Guid.CreateVersion7()}");

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("upper")]
    [InlineData("lower")]
    [InlineData("padded")]
    public async Task List_ByBrand_IgnoresCaseAndSurroundingSpaces(string form)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var brand = $"Kivo{Guid.CreateVersion7():N}"[..20];
        var match = await CreateVisibleProductAsync(admin, category.Id, 1_000_000m, brand);
        await CreateVisibleProductAsync(admin, category.Id, 1_000_000m, "Lumo");
        var asSent = form switch
        {
            "upper" => brand.ToUpperInvariant(),
            "lower" => brand.ToLowerInvariant(),
            _ => $"  {brand}  ",
        };

        var page = await ListAsync(visitor, $"categoryId={category.Id}&brand={Uri.EscapeDataString(asSent)}");

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("minPrice=2000000", new[] { "high", "mid" })] // newest first
    [InlineData("maxPrice=2000000", new[] { "mid", "low" })]
    [InlineData("minPrice=2000000&maxPrice=2000000", new[] { "mid" })] // both bounds are inclusive
    [InlineData("minPrice=1500000&maxPrice=2500000", new[] { "mid" })]
    [InlineData("minPrice=3500000", new string[0])]
    public async Task List_ByPriceRange_ComparesMinPriceInclusive(string range, string[] expected)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var products = new Dictionary<string, ProductBody>
        {
            ["low"] = await CreateVisibleProductAsync(admin, category.Id, 1_000_000m),
            ["mid"] = await CreateVisibleProductAsync(admin, category.Id, 2_000_000m),
            ["high"] = await CreateVisibleProductAsync(admin, category.Id, 3_000_000m),
        };
        // A second, dearer SKU does not move the product: the filter uses the min price.
        await CreateSkuAsync(admin, products["low"].Id, 2_000_000m);

        var page = await ListAsync(visitor, $"categoryId={category.Id}&{range}");

        Assert.Equal(expected.Select(name => products[name].Id), page.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("minPrice=2000000&maxPrice=1000000", "minPrice")]
    [InlineData("minPrice=-1", "minPrice")]
    [InlineData("maxPrice=1.5", "maxPrice")]
    [InlineData("maxPrice=abc", "maxPrice")]
    [InlineData("page=2147483647", "page")] // used to overflow the OFFSET and return 500
    public async Task List_InvalidFilter_Returns400KeyedByQueryName(string query, string expectedKey)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var visitor = factory.CreateApiClient();

        var problem = await ProblemResponse.ReadAsync(await visitor.GetAsync($"/api/products?{query}", Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        Assert.Equal([expectedKey], PropertyNames(problem.Body.GetProperty("errors")));
    }

    [Theory]
    [InlineData("priceAsc", new[] { "cheap", "tieA", "tieB", "dear" })]
    [InlineData("priceDesc", new[] { "dear", "tieB", "tieA", "cheap" })]
    [InlineData("newest", new[] { "tieB", "dear", "tieA", "cheap" })]
    [InlineData("PRICEASC", new[] { "cheap", "tieA", "tieB", "dear" })] // names are not case-sensitive
    public async Task List_Sorted_OrdersByMinPriceOrNewestWithStableTies(string sort, string[] expected)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        // Created in this order, so ids grow in this order too (UUID v7): equal prices are ordered by id.
        var products = new Dictionary<string, ProductBody>
        {
            ["cheap"] = await CreateVisibleProductAsync(admin, category.Id, 1_000_000m),
            ["tieA"] = await CreateVisibleProductAsync(admin, category.Id, 2_000_000m),
            ["dear"] = await CreateVisibleProductAsync(admin, category.Id, 3_000_000m),
            ["tieB"] = await CreateVisibleProductAsync(admin, category.Id, 2_000_000m),
        };

        // Read two at a time: a tie split across pages must neither repeat nor skip a product.
        var first = await ListAsync(visitor, $"categoryId={category.Id}&sort={sort}&pageSize=2");
        var second = await ListAsync(visitor, $"categoryId={category.Id}&sort={sort}&pageSize=2&page=2");

        Assert.Equal(
            expected.Select(name => products[name].Id),
            first.Items.Concat(second.Items).Select(item => item.Id));
    }

    [Theory]
    [InlineData("relevance")] // comes with search in W4-05
    [InlineData("cheapest")]
    [InlineData("99")]
    [InlineData("1")] // a number is not one of the names, even if it matches an enum value
    [InlineData("newest,priceAsc")]
    public async Task List_UnknownSort_Returns400KeyedBySort(string sort)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var visitor = factory.CreateApiClient();

        var problem = await ProblemResponse.ReadAsync(await visitor.GetAsync($"/api/products?sort={sort}", Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        Assert.Equal(["sort"], PropertyNames(problem.Body.GetProperty("errors")));
    }

    private static async Task<ProductBody> CreateVisibleProductAsync(
        HttpClient admin, Guid categoryId, decimal price, string brand = "Novatech")
    {
        var product = await CreateProductAsync(admin, categoryId, brand: brand);
        await CreateSkuAsync(admin, product.Id, price);
        return product;
    }

    private static async Task<PageBody<StorefrontProductBody>> ListAsync(HttpClient visitor, string query)
    {
        var response = await visitor.GetAsync($"/api/products?{query}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PageBody<StorefrontProductBody>>(Token))!;
    }
}
