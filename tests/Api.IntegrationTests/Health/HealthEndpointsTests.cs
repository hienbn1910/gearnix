using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Gearnix.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Live_ProcessRunning_Returns200()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
