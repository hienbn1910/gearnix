using System.Net;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Live_DatabaseUnreachable_Returns200()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_DatabaseReachable_Returns200()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_DatabaseUnreachable_Returns503()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
