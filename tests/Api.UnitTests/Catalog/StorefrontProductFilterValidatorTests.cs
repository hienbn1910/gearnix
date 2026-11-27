using Gearnix.Api.Modules.Catalog.Dtos;
using Gearnix.Api.Modules.Catalog.Validators;

namespace Gearnix.Api.UnitTests.Catalog;

public sealed class StorefrontProductFilterValidatorTests
{
    private readonly StorefrontProductFilterValidator _validator = new();

    [Fact]
    public void EmptyFilter_IsValid()
    {
        Assert.True(_validator.Validate(new StorefrontProductFilter()).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1_000_000_000, true)]
    [InlineData(-1, false)]
    [InlineData(1_000_000_001, false)]
    public void PriceBounds_ValidFrom0UpTo1Billion(long price, bool expectedValid)
    {
        Assert.Equal(expectedValid, _validator.Validate(new StorefrontProductFilter { MinPrice = price }).IsValid);
        Assert.Equal(expectedValid, _validator.Validate(new StorefrontProductFilter { MaxPrice = price }).IsValid);
    }

    [Fact]
    public void FractionalPriceBound_IsInvalid()
    {
        Assert.False(_validator.Validate(new StorefrontProductFilter { MinPrice = 0.5m }).IsValid);
        Assert.False(_validator.Validate(new StorefrontProductFilter { MaxPrice = 0.5m }).IsValid);
    }

    [Theory]
    [InlineData(2_000_000, 2_000_000, true)]
    [InlineData(1_000_000, 2_000_000, true)]
    [InlineData(2_000_000, 1_000_000, false)]
    public void MinAboveMax_IsInvalidOnMinPrice(long min, long max, bool expectedValid)
    {
        var result = _validator.Validate(new StorefrontProductFilter { MinPrice = min, MaxPrice = max });

        Assert.Equal(expectedValid, result.IsValid);
        Assert.All(result.Errors, error => Assert.Equal(nameof(StorefrontProductFilter.MinPrice), error.PropertyName));
    }

    [Fact]
    public void BrandLongerThanAProductBrand_IsInvalid()
    {
        Assert.True(_validator.Validate(new StorefrontProductFilter { Brand = new string('a', 100) }).IsValid);
        Assert.False(_validator.Validate(new StorefrontProductFilter { Brand = new string('a', 101) }).IsValid);
    }

    [Theory]
    [InlineData(ProductSort.Newest, true)]
    [InlineData(ProductSort.PriceAsc, true)]
    [InlineData(ProductSort.PriceDesc, true)]
    [InlineData((ProductSort)99, false)]
    public void Sort_OnlyDefinedValues(ProductSort sort, bool expectedValid)
    {
        Assert.Equal(expectedValid, _validator.Validate(new StorefrontProductFilter { Sort = sort }).IsValid);
    }
}
