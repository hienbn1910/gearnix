using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Security;

public sealed class CsrfTests
{
    private static readonly object ValidBody = new { name = "Nova X", quantity = 1, kind = "Conflict" };

    [Fact]
    public async Task Post_WithoutCsrfHeader_Returns403CsrfHeaderMissing()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var problem = await ProblemResponse.ReadAsync(await client.PostAsJsonAsync(
            "/api/test-errors/validated", ValidBody, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("csrf_header_missing", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }

    [Fact]
    public async Task Post_WithCsrfHeader_PassesThrough()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/test-errors/validated", ValidBody, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutCsrfHeader_PassesThrough()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_PaymentWebhookWithoutCsrfHeader_IsNotRejectedByCsrf()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var problem = await ProblemResponse.ReadAsync(await client.PostAsJsonAsync(
            "/api/payments/webhook", new { }, TestContext.Current.CancellationToken));

        // No webhook endpoint exists yet (W4-02); the point is that CSRF does not answer 403 first.
        Assert.NotEqual("csrf_header_missing", problem.Code);
    }
}
