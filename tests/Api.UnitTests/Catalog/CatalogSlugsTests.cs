using Gearnix.Api.Modules.Catalog;

namespace Gearnix.Api.UnitTests.Catalog;

public sealed class CatalogSlugsTests
{
    [Fact]
    public void PickFree_BaseFree_ReturnsBase()
    {
        Assert.Equal("loa", CatalogSlugs.PickFree("loa", ["loa-2", "loa-bluetooth"]));
    }

    [Fact]
    public void PickFree_BaseTaken_ReturnsFirstFreeSuffix()
    {
        Assert.Equal("loa-3", CatalogSlugs.PickFree("loa", ["loa", "loa-2", "loa-4"]));
    }

    [Fact]
    public void PickFree_OnlyLongerSlugsShareThePrefix_ReturnsBase()
    {
        Assert.Equal("loa", CatalogSlugs.PickFree("loa", ["loa-bluetooth", "loa-bluetooth-2"]));
    }
}
