using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.UnitTests.Common;

public sealed class ValidationErrorKeysTests
{
    [Theory]
    [InlineData("PageSize", "pageSize")]
    [InlineData("pageSize", "pageSize")]
    [InlineData("Items[0].UnitPrice", "items[0].unitPrice")]
    [InlineData("$.name", "$.name")]
    [InlineData("", "")]
    public void ToCamelCase_RewritesEveryPathSegment(string key, string expected)
    {
        var errors = new Dictionary<string, string[]> { [key] = ["message"] };

        ValidationErrorKeys.ToCamelCase(errors);

        Assert.Equal([expected], errors.Keys);
    }

    [Fact]
    public void ToCamelCase_KeysThatBecomeEqual_KeepAllMessages()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["PageSize"] = ["from binding"],
            ["pageSize"] = ["from validator"],
        };

        ValidationErrorKeys.ToCamelCase(errors);

        Assert.Equal(["from binding", "from validator"], Assert.Single(errors).Value);
    }
}
