using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>Many Admin requests at the same moment (Rule.md §9).</summary>
public sealed class CatalogConcurrencyTests(PostgresFixture postgres)
{
    private const int Parallel = 20;

    [Fact]
    public async Task UpdateSku_ParallelPriceChangesOfOneProduct_MinPriceIsTheTrueMinimum()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var skus = new List<SkuBody>();
        for (var i = 0; i < Parallel; i++)
        {
            skus.Add(await CreateSkuAsync(admin, product.Id, 50_000_000m));
        }

        // Every SKU gets a new, different price at the same time; the cheapest new price is 1,000,000.
        var responses = await Task.WhenAll(skus.Select((sku, i) => admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}/skus/{sku.Id}",
            SkuRequest(sku.SkuCode, (i + 1) * 1_000_000m), Token)));
        var after = await GetProductAsync(admin, product.Id);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1_000_000m, after.MinPrice);
    }

    [Fact]
    public async Task CreateSku_ParallelSameSkuCode_ExactlyOneSucceeds()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        // Different products, so the product lock does not line the requests up: only the unique index decides.
        var products = new List<ProductBody>();
        for (var i = 0; i < Parallel; i++)
        {
            products.Add(await CreateProductAsync(admin, category.Id));
        }

        var code = UniqueSkuCode();

        var responses = await Task.WhenAll(products.Select(product => admin.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/skus", SkuRequest(code, 1_000_000m), Token)));
        var problems = await Task.WhenAll(responses
            .Where(response => response.StatusCode != HttpStatusCode.Created)
            .Select(ProblemResponse.ReadAsync));
        var after = await Task.WhenAll(products.Select(product => GetProductAsync(admin, product.Id)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(problems, problem => Assert.Equal("sku_code_taken", problem.Code));
        Assert.Equal(Parallel - 1, problems.Length);
        // Only the winner's product has the SKU and a min price.
        Assert.Single(after, product => product.Skus.Count == 1 && product.MinPrice == 1_000_000m);
        Assert.Equal(Parallel - 1, after.Count(product => product.Skus.Count == 0 && product.MinPrice is null));
    }

    [Fact]
    public async Task CreateCategory_ParallelSameName_DistinctSlugsAndNoServerError()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var name = UniqueName("Cùng tên");

        var responses = await Task.WhenAll(Enumerable.Range(0, Parallel).Select(_ =>
            admin.PostAsJsonAsync("/api/admin/categories", new { name }, Token)));
        var created = await Task.WhenAll(responses
            .Where(response => response.StatusCode == HttpStatusCode.Created)
            .Select(response => response.Content.ReadFromJsonAsync<CategoryBody>(Token)));
        var problems = await Task.WhenAll(responses
            .Where(response => response.StatusCode != HttpStatusCode.Created)
            .Select(ProblemResponse.ReadAsync));

        // Requests that read the same free slug at once lose to the unique index with 409; none fails with 500.
        Assert.NotEmpty(created);
        Assert.All(problems, problem => Assert.Equal("slug_conflict", problem.Code));
        var slugs = created.Select(category => category!.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }
}
