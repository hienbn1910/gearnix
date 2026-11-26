using System.Net;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Errors;

public sealed class ErrorResponseTests
{
    [Fact]
    public async Task UnknownRoute_NoEndpoint_Returns404ProblemWithCode()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var problem = await ProblemResponse.ReadAsync(
            await client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("not_found", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }

    [Fact]
    public async Task UnhandledException_AnyException_Returns500WithoutInternals()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var problem = await ProblemResponse.ReadAsync(
            await client.GetAsync("/api/test-errors/unhandled", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("internal_error", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
        Assert.DoesNotContain("secret internal detail", problem.RawBody);
        Assert.DoesNotContain("InvalidOperationException", problem.RawBody);
    }

    [Fact]
    public async Task DatabaseQuery_DatabaseUnreachable_Returns503()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var problem = await ProblemResponse.ReadAsync(
            await client.GetAsync("/api/test-errors/query-database", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, problem.Status);
        Assert.Equal("database_unavailable", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }
}
