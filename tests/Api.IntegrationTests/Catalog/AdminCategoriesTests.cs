using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.Common.Security;
using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

public sealed class AdminCategoriesTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Create_ValidCategory_Returns201WithLocationAndSlug()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var suffix = Guid.CreateVersion7().ToString("N");

        var response = await admin.PostAsJsonAsync("/api/admin/categories",
            new { name = $"  Điện thoại {suffix}  ", sortOrder = 5 }, Token);
        var body = await response.Content.ReadFromJsonAsync<CategoryBody>(Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal($"/api/admin/categories/{body.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal($"Điện thoại {suffix}", body.Name);
        Assert.Equal($"dien-thoai-{suffix}", body.Slug);
        Assert.Null(body.ParentId);
        Assert.Equal(5, body.SortOrder);
        Assert.Equal(body.CreatedAt, body.UpdatedAt);
    }

    [Fact]
    public async Task Create_SameNameTwice_SecondSlugGetsSuffix()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var name = UniqueName("Phụ kiện");

        var first = await CreateCategoryAsync(admin, name);
        var second = await CreateCategoryAsync(admin, name);
        var third = await CreateCategoryAsync(admin, name);

        Assert.Equal($"{first.Slug}-2", second.Slug);
        Assert.Equal($"{first.Slug}-3", third.Slug);
    }

    [Fact]
    public async Task Get_ExistingCategory_Returns200()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var created = await CreateCategoryAsync(admin);

        var body = await admin.GetFromJsonAsync<CategoryBody>($"/api/admin/categories/{created.Id}", Token);

        Assert.Equal(created, body);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.GetAsync($"/api/admin/categories/{Guid.CreateVersion7()}", Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Fact]
    public async Task List_Categories_PagedAndSortedBySortOrderThenName()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var later = await CreateCategoryAsync(admin, UniqueName("B"), sortOrder: 7);
        var earlierByName = await CreateCategoryAsync(admin, UniqueName("A"), sortOrder: 7);
        var earlierBySortOrder = await CreateCategoryAsync(admin, UniqueName("C"), sortOrder: 6);

        var page = await admin.GetFromJsonAsync<PageBody<CategoryBody>>(
            "/api/admin/categories?page=1&pageSize=100", Token);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
        var ids = page.Items.Select(category => category.Id).ToList();
        Assert.True(ids.IndexOf(earlierBySortOrder.Id) < ids.IndexOf(earlierByName.Id));
        Assert.True(ids.IndexOf(earlierByName.Id) < ids.IndexOf(later.Id));
        Assert.Equal(page.Items.Select(category => category.SortOrder).Order(), page.Items.Select(c => c.SortOrder));
    }

    [Fact]
    public async Task List_PageSizeOverMaximum_Returns400()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.GetAsync("/api/admin/categories?pageSize=101", Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
    }

    [Theory]
    [InlineData("", 0, "name")]
    [InlineData("   ", 0, "name")]
    [InlineData("Hợp lệ", -1, "sortOrder")]
    [InlineData("Hợp lệ", 1001, "sortOrder")]
    public async Task Create_InvalidField_Returns400WithFieldError(string name, int sortOrder, string field)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(
            await admin.PostAsJsonAsync("/api/admin/categories", new { name, sortOrder }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.True(problem.Body.GetProperty("errors").TryGetProperty(field, out _), problem.RawBody);
    }

    [Fact]
    public async Task Create_NameOver100Characters_Returns400()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var response = await admin.PostAsJsonAsync("/api/admin/categories", new { name = new string('a', 101) }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ParentIsTopLevel_Returns201WithParent()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parent = await CreateCategoryAsync(admin);

        var child = await CreateCategoryAsync(admin, parentId: parent.Id);

        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public async Task Create_ParentIsSubCategory_Returns400InvalidCategoryParent()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parent = await CreateCategoryAsync(admin);
        var child = await CreateCategoryAsync(admin, parentId: parent.Id);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync("/api/admin/categories",
            new { name = UniqueName("Cấp ba"), parentId = child.Id }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_category_parent", problem.Code);
    }

    [Fact]
    public async Task Create_UnknownParent_Returns400InvalidCategoryParent()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync("/api/admin/categories",
            new { name = UniqueName("Mồ côi"), parentId = Guid.CreateVersion7() }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_category_parent", problem.Code);
    }

    [Fact]
    public async Task Update_NewNameParentAndSortOrder_Returns200AndKeepsSlug()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parent = await CreateCategoryAsync(admin);
        var category = await CreateCategoryAsync(admin);
        var newName = UniqueName("Tên mới");

        var response = await admin.PutAsJsonAsync($"/api/admin/categories/{category.Id}",
            new { name = $" {newName} ", parentId = parent.Id, sortOrder = 9 }, Token);
        var body = await response.Content.ReadFromJsonAsync<CategoryBody>(Token);
        var reread = await admin.GetFromJsonAsync<CategoryBody>($"/api/admin/categories/{category.Id}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(newName, body.Name);
        Assert.Equal(category.Slug, body.Slug);
        Assert.Equal(parent.Id, body.ParentId);
        Assert.Equal(9, body.SortOrder);
        Assert.Equal(category.CreatedAt, body.CreatedAt);
        Assert.True(body.UpdatedAt >= category.UpdatedAt);
        Assert.Equal(body, reread);
    }

    [Fact]
    public async Task Update_RemoveParent_Returns200TopLevel()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parent = await CreateCategoryAsync(admin);
        var child = await CreateCategoryAsync(admin, parentId: parent.Id);

        var body = await (await admin.PutAsJsonAsync($"/api/admin/categories/{child.Id}",
            new { name = child.Name, parentId = (Guid?)null, sortOrder = 0 }, Token))
            .Content.ReadFromJsonAsync<CategoryBody>(Token);

        Assert.NotNull(body);
        Assert.Null(body.ParentId);
    }

    [Fact]
    public async Task Update_OwnParent_Returns400InvalidCategoryParent()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var category = await CreateCategoryAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/categories/{category.Id}", new { name = category.Name, parentId = category.Id }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_category_parent", problem.Code);
    }

    [Fact]
    public async Task Update_CategoryWithChildrenGetsParent_Returns400InvalidCategoryParent()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        var parentWithChild = await CreateCategoryAsync(admin);
        await CreateCategoryAsync(admin, parentId: parentWithChild.Id);
        var otherTopLevel = await CreateCategoryAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/categories/{parentWithChild.Id}",
            new { name = parentWithChild.Name, parentId = otherTopLevel.Id }, Token));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_category_parent", problem.Code);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            $"/api/admin/categories/{Guid.CreateVersion7()}", new { name = "Không có" }, Token));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Fact]
    public async Task Create_WithoutCsrfHeader_Returns403CsrfHeaderMissing()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        admin.DefaultRequestHeaders.Remove(CsrfHeaderMiddleware.HeaderName);

        var problem = await ProblemResponse.ReadAsync(
            await admin.PostAsJsonAsync("/api/admin/categories", new { name = UniqueName("CSRF") }, Token));

        Assert.Equal(HttpStatusCode.Forbidden, problem.Status);
        Assert.Equal("csrf_header_missing", problem.Code);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("WarehouseStaff")]
    public async Task Endpoints_NotAdmin_Return403(string role)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClientAs(Guid.CreateVersion7(), role);

        var list = await client.GetAsync("/api/admin/categories", Token);
        var create = await client.PostAsJsonAsync("/api/admin/categories", new { name = "X" }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Endpoints_Anonymous_Return401()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/admin/categories", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
