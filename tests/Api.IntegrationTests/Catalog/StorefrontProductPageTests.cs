using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>The public product page, found by slug.</summary>
public sealed class StorefrontProductPageTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Get_VisibleProduct_ReturnsProductWithActiveSkusCheapestFirst()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var product = await CreateProductAsync(admin, category.Id);
        var dear = await CreateSkuAsync(admin, product.Id, 15_000_000m);
        await CreateSkuAsync(admin, product.Id, 5_000_000m, isActive: false);
        var cheap = await CreateSkuAsync(admin, product.Id, 12_000_000m);

        var response = await visitor.GetAsync($"/api/products/{product.Slug}", Token);
        var page = await response.Content.ReadFromJsonAsync<StorefrontProductPageBody>(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(product.Id, page!.Id);
        Assert.Equal(product.Slug, page.Slug);
        Assert.Equal(product.Name, page.Name);
        Assert.Equal("Novatech", page.Brand);
        Assert.Equal("Mô tả", page.Description);
        Assert.Equal("N1", page.Specs["cpu"].GetString());
        Assert.Equal(8, page.Specs["ramGb"].GetInt32());
        Assert.Equal(12_000_000m, page.MinPrice);
        Assert.Equal(new StorefrontCategoryBody(category.Id, category.Name, category.Slug), page.Category);
        Assert.Equal([cheap.Id, dear.Id], page.Skus.Select(sku => sku.Id));
        var first = page.Skus[0];
        Assert.Equal(cheap.SkuCode, first.SkuCode);
        Assert.Equal("128GB - Đen", first.VariantName);
        Assert.Equal(12_000_000m, first.Price);
        Assert.Equal("Đen", first.Attributes["color"]);
    }

    [Fact]
    public async Task Get_EqualSkuPrices_OrderedById()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var skus = new[]
        {
            await CreateSkuAsync(admin, product.Id, 1_000_000m),
            await CreateSkuAsync(admin, product.Id, 1_000_000m),
            await CreateSkuAsync(admin, product.Id, 1_000_000m),
        };

        var page = await visitor.GetFromJsonAsync<StorefrontProductPageBody>($"/api/products/{product.Slug}", Token);

        Assert.Equal(skus.Select(sku => sku.Id).Order(), page!.Skus.Select(sku => sku.Id));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("noActiveSku")]
    [InlineData("noSku")]
    [InlineData("unknown")]
    public async Task Get_HiddenOrUnknownProduct_Returns404(string kind)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var category = await CreateCategoryAsync(admin);
        var slug = kind switch
        {
            "inactive" => await HiddenAsync(isActive: false, skuActive: true),
            "noActiveSku" => await HiddenAsync(isActive: true, skuActive: false),
            "noSku" => (await CreateProductAsync(admin, category.Id)).Slug,
            _ => $"khong-co-{Guid.CreateVersion7():N}",
        };

        var problem = await ProblemResponse.ReadAsync(await visitor.GetAsync($"/api/products/{slug}", Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);

        async Task<string> HiddenAsync(bool isActive, bool skuActive)
        {
            var product = await CreateProductAsync(admin, category.Id, isActive: isActive);
            await CreateSkuAsync(admin, product.Id, 1_000_000m, isActive: skuActive);
            return product.Slug;
        }
    }

    [Fact]
    public async Task Get_ResponseHasOnlyPublicFields()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        await CreateSkuAsync(admin, product.Id, 1_000_000m);

        var json = await ReadJsonAsync(await visitor.GetAsync($"/api/products/{product.Slug}", Token));

        Assert.Equal(
            ["brand", "category", "description", "id", "minPrice", "name", "skus", "slug", "specs"],
            PropertyNames(json).Order());
        Assert.Equal(["id", "name", "slug"], PropertyNames(json.GetProperty("category")).Order());
        Assert.Equal(
            ["attributes", "id", "price", "skuCode", "variantName"],
            PropertyNames(json.GetProperty("skus")[0]).Order());
    }
}
