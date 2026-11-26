using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.UnitTests.Common;

public sealed class ErrorCodesTests
{
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(409, "conflict")]
    [InlineData(429, "rate_limited")]
    [InlineData(500, "internal_error")]
    [InlineData(503, "service_unavailable")]
    public void ForStatus_ListedStatus_ReturnsItsCode(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, ErrorCodes.ForStatus(status));
    }

    [Theory]
    [InlineData(406)]
    [InlineData(413)]
    [InlineData(415)]
    public void ForStatus_UnlistedClientError_ReturnsBadRequest(int status)
    {
        Assert.Equal("bad_request", ErrorCodes.ForStatus(status));
    }

    [Theory]
    [InlineData(501)]
    [InlineData(502)]
    [InlineData(504)]
    public void ForStatus_UnlistedServerError_ReturnsInternalError(int status)
    {
        Assert.Equal("internal_error", ErrorCodes.ForStatus(status));
    }
}
