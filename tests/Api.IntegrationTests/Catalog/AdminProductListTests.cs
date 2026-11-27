using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>
/// The admin product list. Every test filters by its own new category, because all tests share one database.
/// </summary>
public sealed class AdminProductListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task List_ByCategory_NewestFirstWithSkuCountAndMinPrice()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var older = await CreateProductAsync(admin, category.Id);
        var newer = await CreateProductAsync(admin, category.Id);
        await CreateSkuAsync(admin, newer.Id, 15_000_000m);
        await CreateSkuAsync(admin, newer.Id, 12_000_000m);

        var page = await ListAsync(admin, $"categoryId={category.Id}");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(item => item.Id));
        var first = page.Items[0];
        Assert.Equal(newer.Name, first.Name);
        Assert.Equal(newer.Slug, first.Slug);
        Assert.Equal("Novatech", first.Brand);
        Assert.Equal(category.Id, first.CategoryId);
        Assert.Equal(2, first.SkuCount);
        Assert.Equal(12_000_000m, first.MinPrice);
        Assert.True(first.IsActive);
        Assert.Equal(0, page.Items[1].SkuCount);
        Assert.Null(page.Items[1].MinPrice);
    }

    [Fact]
    public async Task List_SecondPage_ReturnsRemainingItems()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var first = await CreateProductAsync(admin, category.Id);
        await CreateProductAsync(admin, category.Id);
        await CreateProductAsync(admin, category.Id);

        var page = await ListAsync(admin, $"categoryId={category.Id}&page=2&pageSize=2");

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(first.Id, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("tai nghe")]
    [InlineData("TAI NGHE")]
    [InlineData("Tai Nghe Không Dây")]
    [InlineData("khong day")]
    public async Task List_QueryWithOrWithoutDiacritics_FindsProduct(string q)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var match = await CreateProductAsync(admin, category.Id, UniqueName("Tai nghe không dây"));
        await CreateProductAsync(admin, category.Id, UniqueName("Loa"));

        var page = await ListAsync(admin, $"categoryId={category.Id}&q={Uri.EscapeDataString(q)}");

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task List_QueryWithLikeWildcards_MatchesThemLiterally()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var match = await CreateProductAsync(admin, category.Id, UniqueName("Pin 100%"));
        await CreateProductAsync(admin, category.Id, UniqueName("Pin 1000"));

        var page = await ListAsync(admin, $"categoryId={category.Id}&q={Uri.EscapeDataString("100%")}");

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task List_ByIsActive_ReturnsOnlyMatching(bool isActive)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var active = await CreateProductAsync(admin, category.Id, isActive: true);
        var inactive = await CreateProductAsync(admin, category.Id, isActive: false);

        var page = await ListAsync(admin, $"categoryId={category.Id}&isActive={isActive}");

        Assert.Equal(isActive ? active.Id : inactive.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task List_QueryOver200Characters_Returns400()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.GetAsync($"/api/admin/products?q={new string('a', 201)}", Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.True(problem.Body.GetProperty("errors").TryGetProperty("q", out _), problem.RawBody);
    }

    private static async Task<PageBody<ProductListItemBody>> ListAsync(HttpClient admin, string queryString) =>
        await admin.GetFromJsonAsync<PageBody<ProductListItemBody>>($"/api/admin/products?{queryString}", Token)
        ?? throw new InvalidOperationException("Empty list body.");
}
