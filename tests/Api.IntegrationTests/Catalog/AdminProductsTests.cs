using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

public sealed class AdminProductsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Create_ValidProduct_Returns201WithSlugSpecsAndNoMinPrice()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var suffix = Guid.CreateVersion7().ToString("N");

        var response = await admin.PostAsJsonAsync("/api/admin/products", new
        {
            categoryId = category.Id,
            name = $" Điện thoại Nova X 5G {suffix} ",
            brand = " Novatech ",
            description = "Màn hình 6.5 inch",
            specs = new { cpu = "N1", ramGb = 16, nfc = true },
        }, Token);
        var body = await response.Content.ReadFromJsonAsync<ProductBody>(Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal($"/api/admin/products/{body.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal($"Điện thoại Nova X 5G {suffix}", body.Name);
        Assert.Equal($"dien-thoai-nova-x-5g-{suffix}", body.Slug);
        Assert.Equal("Novatech", body.Brand);
        Assert.Equal(category.Id, body.CategoryId);
        Assert.Equal("N1", body.Specs["cpu"].GetString());
        Assert.Equal(16, body.Specs["ramGb"].GetInt32());
        Assert.True(body.Specs["nfc"].GetBoolean());
        Assert.Null(body.MinPrice);
        Assert.True(body.IsActive); // default
        Assert.Empty(body.Skus);
    }

    [Fact]
    public async Task Create_WithoutDescriptionAndSpecs_Returns201WithEmptyValues()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/admin/products",
            new { categoryId = category.Id, name = UniqueName("Chuột"), brand = "Kivo", isActive = false }, Token);
        var body = await response.Content.ReadFromJsonAsync<ProductBody>(Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("", body.Description);
        Assert.Empty(body.Specs);
        Assert.False(body.IsActive);
    }

    [Fact]
    public async Task Create_SubCategory_Returns201()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parent = await CreateCategoryAsync(admin);
        var child = await CreateCategoryAsync(admin, parentId: parent.Id);

        var product = await CreateProductAsync(admin, child.Id);

        Assert.Equal(child.Id, product.CategoryId);
    }

    [Fact]
    public async Task Create_UnknownCategory_Returns400CategoryNotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync("/api/admin/products",
            new { categoryId = Guid.CreateVersion7(), name = UniqueName("Lạc"), brand = "Kivo" }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("category_not_found", problem.Code);
    }

    [Fact]
    public async Task Create_SameNameTwice_SecondSlugGetsSuffix()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var name = UniqueName("Bàn phím");

        var first = await CreateProductAsync(admin, category.Id, name);
        var second = await CreateProductAsync(admin, category.Id, name);

        Assert.Equal($"{first.Slug}-2", second.Slug);
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_InvalidField_Returns400WithFieldError(string field, object body)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync("/api/admin/products", body, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        Assert.True(problem.Body.GetProperty("errors").TryGetProperty(field, out _), problem.RawBody);
    }

    public static TheoryData<string, object> InvalidBodies()
    {
        var categoryId = Guid.CreateVersion7();
        return new()
        {
            { "categoryId", new { name = "Tên", brand = "Kivo" } },
            { "name", new { categoryId, name = " ", brand = "Kivo" } },
            { "name", new { categoryId, name = new string('a', 201), brand = "Kivo" } },
            { "brand", new { categoryId, name = "Tên", brand = "" } },
            { "brand", new { categoryId, name = "Tên", brand = new string('b', 101) } },
            { "description", new { categoryId, name = "Tên", brand = "Kivo", description = new string('d', 5001) } },
            { "specs", new { categoryId, name = "Tên", brand = "Kivo", specs = new { nested = new { a = 1 } } } },
            { "specs", new { categoryId, name = "Tên", brand = "Kivo", specs = new { list = new[] { 1, 2 } } } },
            { "specs", new { categoryId, name = "Tên", brand = "Kivo", specs = new { empty = (string?)null } } },
            { "specs", new { categoryId, name = "Tên", brand = "Kivo", specs = new { cpu = new string('c', 201) } } },
            {
                "specs",
                new
                {
                    categoryId, name = "Tên", brand = "Kivo",
                    specs = Enumerable.Range(0, 31).ToDictionary(i => $"key{i}", i => (object)i),
                }
            },
            {
                "specs",
                new
                {
                    categoryId, name = "Tên", brand = "Kivo",
                    specs = new Dictionary<string, object> { [new string('k', 51)] = 1 },
                }
            },
        };
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.GetAsync($"/api/admin/products/{Guid.CreateVersion7()}", Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Fact]
    public async Task Get_ExistingProduct_ReturnsSameAsCreated()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var created = await CreateProductAsync(admin, category.Id);

        var body = await GetProductAsync(admin, created.Id);

        Assert.Equal(created.Name, body.Name);
        Assert.Equal(created.Slug, body.Slug);
        Assert.Equal(created.CreatedAt, body.CreatedAt);
        Assert.Equal(created.Specs["cpu"].GetString(), body.Specs["cpu"].GetString());
        Assert.Equal(created.Specs["ramGb"].GetInt32(), body.Specs["ramGb"].GetInt32());
    }

    [Fact]
    public async Task Update_AllFields_Returns200KeepsSlugAndRenameIsSearchable()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var otherCategory = await CreateCategoryAsync(admin);
        var product = await CreateProductAsync(admin, category.Id);
        var suffix = Guid.CreateVersion7().ToString("N");

        var response = await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", new
        {
            categoryId = otherCategory.Id,
            name = $"Đồng hồ thông minh {suffix}",
            brand = "Orbita",
            description = "Mới",
            specs = new { battery = "7 ngày" },
            isActive = false,
        }, Token);
        var body = await response.Content.ReadFromJsonAsync<ProductBody>(Token);
        var found = await admin.GetFromJsonAsync<PageBody<ProductListItemBody>>(
            $"/api/admin/products?q=dong ho thong minh {suffix}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(product.Slug, body.Slug);
        Assert.Equal(otherCategory.Id, body.CategoryId);
        Assert.Equal($"Đồng hồ thông minh {suffix}", body.Name);
        Assert.Equal("Orbita", body.Brand);
        Assert.Equal("Mới", body.Description);
        Assert.Equal(["battery"], body.Specs.Keys);
        Assert.False(body.IsActive);
        Assert.Equal(product.CreatedAt, body.CreatedAt);
        Assert.NotNull(found);
        Assert.Equal(product.Id, Assert.Single(found.Items).Id);
    }

    [Fact]
    public async Task Update_UnknownCategory_Returns400CategoryNotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);
        var product = await CreateProductAsync(admin, category.Id);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}",
            new { categoryId = Guid.CreateVersion7(), name = product.Name, brand = product.Brand }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("category_not_found", problem.Code);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/products/{Guid.CreateVersion7()}",
            new { categoryId = category.Id, name = "Không có", brand = "Kivo" }, Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("WarehouseStaff")]
    public async Task Endpoints_NotAdmin_Return403(string role)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClientAs(Guid.CreateVersion7(), role);
        var productId = Guid.CreateVersion7();

        var list = await client.GetAsync("/api/admin/products", Token);
        var create = await client.PostAsJsonAsync("/api/admin/products", new { name = "X" }, Token);
        var createSku = await client.PostAsJsonAsync($"/api/admin/products/{productId}/skus", new { }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createSku.StatusCode);
    }

    [Fact]
    public async Task Endpoints_Anonymous_Return401()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        var list = await client.GetAsync("/api/admin/products", Token);
        var createSku = await client.PostAsJsonAsync($"/api/admin/products/{Guid.CreateVersion7()}/skus", new { }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, createSku.StatusCode);
    }
}
