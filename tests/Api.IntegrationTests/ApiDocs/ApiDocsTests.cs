using System.Net;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.ApiDocs;

public sealed class ApiDocsTests
{
    [Fact]
    public async Task OpenApiDocument_Development_Returns200WithProblemDetailsAndStringEnums()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString, "Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", document);
        Assert.Contains("\"ProblemDetails\"", document);
        Assert.Contains("\"Conflict\"", document); // ErrorKind values listed as strings
    }

    [Fact]
    public async Task DocsPage_Development_Returns200()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString, "Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/docs", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/openapi/v1.json")]
    [InlineData("/api/docs")]
    public async Task ApiDocs_TestEnvironment_Returns404(string path)
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
