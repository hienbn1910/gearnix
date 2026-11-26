using Gearnix.Api.Common.Paging;

namespace Gearnix.Api.UnitTests.Common;

public sealed class PageRequestValidatorTests
{
    private readonly PageRequestValidator _validator = new();

    [Fact]
    public void New_NoValuesGiven_DefaultsToPage1Size20()
    {
        var request = new PageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 100)]
    public void Validate_WithinBounds_IsValid(int page, int pageSize)
    {
        var result = _validator.Validate(new PageRequest { Page = page, PageSize = pageSize });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(-1, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Validate_OutOfBounds_FailsOnThatField(int page, int pageSize, string invalidField)
    {
        var result = _validator.Validate(new PageRequest { Page = page, PageSize = pageSize });

        var failure = Assert.Single(result.Errors);
        Assert.Equal(invalidField, failure.PropertyName);
    }
}
