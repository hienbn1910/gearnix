using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

using static Gearnix.Api.IntegrationTests.Catalog.CatalogApi;

namespace Gearnix.Api.IntegrationTests.Catalog;

/// <summary>
/// The public category tree. Other tests add categories to the same database, so each test looks only at its own.
/// </summary>
public sealed class StorefrontCategoriesTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Tree_Anonymous_TopLevelWithChildrenInSortOrderThenName()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var later = await CreateCategoryAsync(admin, sortOrder: 900);
        var earlier = await CreateCategoryAsync(admin, sortOrder: 899);
        var childB = await CreateCategoryAsync(admin, UniqueName("B"), later.Id, sortOrder: 1);
        var childA = await CreateCategoryAsync(admin, UniqueName("A"), later.Id, sortOrder: 1);
        var childFirst = await CreateCategoryAsync(admin, UniqueName("C"), later.Id, sortOrder: 0);

        var response = await visitor.GetAsync("/api/categories", Token);
        var tree = await response.Content.ReadFromJsonAsync<CategoryTreeBody>(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = tree!.Items.Select(item => item.Id).ToList();
        Assert.True(ids.IndexOf(earlier.Id) < ids.IndexOf(later.Id));
        Assert.DoesNotContain(childA.Id, ids); // sub-categories appear only under their parent
        var node = tree.Items.Single(item => item.Id == later.Id);
        Assert.Equal(later.Name, node.Name);
        Assert.Equal(later.Slug, node.Slug);
        Assert.Equal([childFirst.Id, childA.Id, childB.Id], node.Children.Select(child => child.Id));
        Assert.Equal(childA.Slug, node.Children[1].Slug);
        Assert.Empty(tree.Items.Single(item => item.Id == earlier.Id).Children);
    }

    [Fact]
    public async Task Tree_ResponseHasOnlyPublicFields()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");
        using var visitor = factory.CreateApiClient();
        var parent = await CreateCategoryAsync(admin);
        await CreateCategoryAsync(admin, parentId: parent.Id);

        var json = await ReadJsonAsync(await visitor.GetAsync("/api/categories", Token));

        Assert.Equal(["items"], PropertyNames(json));
        var node = json.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == parent.Id);
        Assert.Equal(["children", "id", "name", "slug"], PropertyNames(node).Order());
        Assert.Equal(["id", "name", "slug"], PropertyNames(node.GetProperty("children")[0]).Order());
    }
}
