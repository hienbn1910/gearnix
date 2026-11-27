using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

public sealed class AdminSkusTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Create_ValidSku_Returns201AndSetsMinPrice()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var code = UniqueSkuCode();

        var response = await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/skus", new
        {
            skuCode = $"  {code.ToLowerInvariant()} ",
            variantName = " 128GB - Đen ",
            price = 12_990_000m,
            attributes = new { storage = "128GB", color = "Đen" },
        }, Token);
        var sku = await response.Content.ReadFromJsonAsync<SkuBody>(Token);
        var reread = await GetProductAsync(admin, product.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(sku);
        Assert.Equal($"/api/admin/products/{product.Id}/skus/{sku.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(product.Id, sku.ProductId);
        Assert.Equal(code, sku.SkuCode); // stored upper case and trimmed
        Assert.Equal("128GB - Đen", sku.VariantName);
        Assert.Equal(12_990_000m, sku.Price);
        Assert.Equal("Đen", sku.Attributes["color"]);
        Assert.True(sku.IsActive);
        Assert.Equal(12_990_000m, reread.MinPrice);
        Assert.Equal(sku.Id, Assert.Single(reread.Skus).Id);
    }

    [Fact]
    public async Task Create_InactiveSku_DoesNotCountForMinPrice()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);

        await CreateSkuAsync(admin, product.Id, 5_000_000m, isActive: false);
        var afterInactiveOnly = await GetProductAsync(admin, product.Id);
        await CreateSkuAsync(admin, product.Id, 9_000_000m);
        var afterActive = await GetProductAsync(admin, product.Id);

        Assert.Null(afterInactiveOnly.MinPrice);
        Assert.Equal(9_000_000m, afterActive.MinPrice);
    }

    [Fact]
    public async Task Update_PriceAndActiveFlag_RecomputesMinPrice()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var cheap = await CreateSkuAsync(admin, product.Id, 10_000_000m);
        var expensive = await CreateSkuAsync(admin, product.Id, 20_000_000m);

        // The cheap SKU becomes the most expensive one.
        var repriced = await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}/skus/{cheap.Id}",
            SkuRequest(cheap.SkuCode, 25_000_000m), Token);
        var afterReprice = await GetProductAsync(admin, product.Id);

        // Deactivating the now-cheapest SKU leaves only the other one.
        await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}/skus/{expensive.Id}",
            SkuRequest(expensive.SkuCode, 20_000_000m, isActive: false), Token);
        var afterDeactivate = await GetProductAsync(admin, product.Id);

        // Deactivating the last active SKU leaves no min price.
        await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}/skus/{cheap.Id}",
            SkuRequest(cheap.SkuCode, 25_000_000m, isActive: false), Token);
        var afterNoneActive = await GetProductAsync(admin, product.Id);

        Assert.Equal(HttpStatusCode.OK, repriced.StatusCode);
        Assert.Equal(20_000_000m, afterReprice.MinPrice);
        Assert.Equal(25_000_000m, afterDeactivate.MinPrice);
        Assert.Null(afterNoneActive.MinPrice);
    }

    [Fact]
    public async Task Update_AllFields_Returns200WithNewValues()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var sku = await CreateSkuAsync(admin, product.Id, 10_000_000m);
        var newCode = UniqueSkuCode();

        var response = await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}/skus/{sku.Id}", new
        {
            skuCode = newCode,
            variantName = "256GB - Trắng",
            price = 11_000_000m,
            attributes = new { storage = "256GB", color = "Trắng" },
            isActive = true,
        }, Token);
        var body = await response.Content.ReadFromJsonAsync<SkuBody>(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(newCode, body.SkuCode);
        Assert.Equal("256GB - Trắng", body.VariantName);
        Assert.Equal(11_000_000m, body.Price);
        Assert.Equal("256GB", body.Attributes["storage"]);
        Assert.Equal(sku.CreatedAt, body.CreatedAt);
    }

    [Fact]
    public async Task Create_SkuCodeTakenInAnyCase_Returns409SkuCodeTaken()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var existing = await CreateSkuAsync(admin, (await CreateProductAsync(admin, category.Id)).Id, 1_000_000m);
        var otherProduct = await CreateProductAsync(admin, category.Id);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            $"/api/admin/products/{otherProduct.Id}/skus",
            SkuRequest(existing.SkuCode.ToLowerInvariant(), 2_000_000m), Token));
        var otherAfter = await GetProductAsync(admin, otherProduct.Id);

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal("sku_code_taken", problem.Code);
        Assert.Empty(otherAfter.Skus);
        Assert.Null(otherAfter.MinPrice);
    }

    [Fact]
    public async Task Update_ToTakenSkuCode_Returns409SkuCodeTaken()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);
        var first = await CreateSkuAsync(admin, product.Id, 1_000_000m);
        var second = await CreateSkuAsync(admin, product.Id, 2_000_000m);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}/skus/{second.Id}", SkuRequest(first.SkuCode, 500_000m), Token));
        var after = await GetProductAsync(admin, product.Id);

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal("sku_code_taken", problem.Code);
        Assert.Equal(1_000_000m, after.MinPrice); // nothing from the failed update was saved
    }

    [Fact]
    public async Task Create_UnknownProduct_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            $"/api/admin/products/{Guid.CreateVersion7()}/skus", SkuRequest(UniqueSkuCode(), 1_000_000m), Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Fact]
    public async Task Update_SkuOfAnotherProduct_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var owner = await CreateProductAsync(admin, category.Id);
        var other = await CreateProductAsync(admin, category.Id);
        var sku = await CreateSkuAsync(admin, owner.Id, 1_000_000m);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/products/{other.Id}/skus/{sku.Id}", SkuRequest(sku.SkuCode, 2_000_000m), Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
        Assert.Equal(1_000_000m, (await GetProductAsync(admin, owner.Id)).MinPrice);
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_InvalidField_Returns400WithFieldError(string field, object body)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.PostAsJsonAsync($"/api/admin/products/{Guid.CreateVersion7()}/skus", body, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        Assert.True(problem.Body.GetProperty("errors").TryGetProperty(field, out _), problem.RawBody);
    }

    public static TheoryData<string, object> InvalidBodies() => new()
    {
        { "skuCode", new { skuCode = "AB", variantName = "V", price = 1m } },
        { "skuCode", new { skuCode = new string('A', 51), variantName = "V", price = 1m } },
        { "skuCode", new { skuCode = "NOVA X", variantName = "V", price = 1m } },
        { "skuCode", new { skuCode = "-NOVAX", variantName = "V", price = 1m } },
        { "skuCode", new { skuCode = "NOVA--X", variantName = "V", price = 1m } },
        { "skuCode", new { skuCode = "ĐIỆN-01", variantName = "V", price = 1m } },
        { "variantName", new { skuCode = "NOVAX-1", variantName = " ", price = 1m } },
        { "variantName", new { skuCode = "NOVAX-1", variantName = new string('v', 101), price = 1m } },
        { "price", new { skuCode = "NOVAX-1", variantName = "V", price = 0m } },
        { "price", new { skuCode = "NOVAX-1", variantName = "V", price = -1m } },
        { "price", new { skuCode = "NOVAX-1", variantName = "V", price = 1_000_000_001m } },
        { "price", new { skuCode = "NOVAX-1", variantName = "V", price = 99_999.5m } },
        {
            "attributes",
            new
            {
                skuCode = "NOVAX-1", variantName = "V", price = 1m,
                attributes = Enumerable.Range(0, 11).ToDictionary(i => $"key{i}", i => $"{i}"),
            }
        },
        {
            "attributes",
            new { skuCode = "NOVAX-1", variantName = "V", price = 1m, attributes = new { color = new string('c', 201) } }
        },
    };

    [Fact]
    public async Task Create_MaximumPrice_Returns201()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);

        var sku = await CreateSkuAsync(admin, product.Id, 1_000_000_000m);

        Assert.Equal(1_000_000_000m, sku.Price);
    }

    [Fact]
    public async Task Update_UnknownSku_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var product = await CreateProductAsync(admin, (await CreateCategoryAsync(admin)).Id);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}/skus/{Guid.CreateVersion7()}",
            SkuRequest(UniqueSkuCode(), 1_000_000m), Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }
}
