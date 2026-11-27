using System.Text.Json;

using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Validators;

namespace Gearnix.Api.UnitTests.Catalog;

public sealed class CatalogValidatorsTests
{
    private static readonly SkuRequest ValidSku = new("NOVAX-128-BLK", "128GB - Đen", 12_990_000m, null);

    private readonly SkuRequestValidator _sku = new();

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000_000, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1_000_000_001, false)]
    public void Sku_PriceRange_ValidOnlyAbove0UpTo1Billion(long price, bool expectedValid)
    {
        Assert.Equal(expectedValid, _sku.Validate(ValidSku with { Price = price }).IsValid);
    }

    [Fact]
    public void Sku_FractionalPrice_IsInvalid()
    {
        Assert.False(_sku.Validate(ValidSku with { Price = 12_990_000.5m }).IsValid);
    }

    [Theory]
    [InlineData("novax-128-blk", true)] // upper-cased before checking
    [InlineData("  ABC  ", true)]
    [InlineData("A1", false)]
    [InlineData("NOVA_X", false)]
    [InlineData("NOVAX-", false)]
    [InlineData("NOVA--X", false)]
    public void Sku_SkuCodeFormat_ValidOnlyLettersDigitsInnerHyphens(string skuCode, bool expectedValid)
    {
        Assert.Equal(expectedValid, _sku.Validate(ValidSku with { SkuCode = skuCode }).IsValid);
    }

    [Fact]
    public void Attributes_TenKeys_IsValidButElevenIsNot()
    {
        Assert.True(CatalogJsonRules.AreValidAttributes(Attributes(10)));
        Assert.False(CatalogJsonRules.AreValidAttributes(Attributes(11)));
    }

    [Fact]
    public void Attributes_KeysEqualAfterTrimming_AreInvalid()
    {
        Assert.False(CatalogJsonRules.AreValidAttributes(new() { ["color"] = "Đen", [" color "] = "Trắng" }));
    }

    [Theory]
    [InlineData("\"N1\"", true)]
    [InlineData("16", true)]
    [InlineData("2.5", true)]
    [InlineData("true", true)]
    [InlineData("null", false)]
    [InlineData("[1]", false)]
    [InlineData("{\"a\":1}", false)]
    public void Specs_ValueKind_OnlyStringsNumbersAndBooleans(string json, bool expectedValid)
    {
        var specs = new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.Deserialize<JsonElement>(json) };

        Assert.Equal(expectedValid, CatalogJsonRules.AreValidSpecs(specs));
    }

    [Fact]
    public void Specs_ThirtyKeys_IsValidButThirtyOneIsNot()
    {
        Assert.True(CatalogJsonRules.AreValidSpecs(Specs(30)));
        Assert.False(CatalogJsonRules.AreValidSpecs(Specs(31)));
    }

    private static Dictionary<string, string> Attributes(int count) =>
        Enumerable.Range(0, count).ToDictionary(i => $"key{i}", i => $"{i}");

    private static Dictionary<string, JsonElement> Specs(int count) =>
        Enumerable.Range(0, count).ToDictionary(i => $"key{i}", i => JsonSerializer.SerializeToElement(i));
}
