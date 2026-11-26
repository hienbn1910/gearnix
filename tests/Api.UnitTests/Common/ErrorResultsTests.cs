using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.UnitTests.Common;

public sealed class ErrorResultsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.Forbidden, 403)]
    public void StatusCodeFor_EachKind_ReturnsAgreedStatus(ErrorKind kind, int expectedStatus)
    {
        Assert.Equal(expectedStatus, ErrorResults.StatusCodeFor(kind));
    }
}
