using System.Net;
using System.Net.Http.Json;
using System.Text;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Errors;

public sealed class ValidationTests
{
    private const string Path = "/api/test-errors/validated";

    [Fact]
    public async Task Post_InvalidBody_Returns400WithFieldErrors()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Path, new { name = "", quantity = 0, kind = "Conflict" }, TestContext.Current.CancellationToken);
        var problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("validation_failed", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
        var errors = problem.Body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _), problem.RawBody);
        Assert.True(errors.TryGetProperty("quantity", out _), problem.RawBody);
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400ValidationFailed()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        using var content = new StringContent("{ \"name\": ", Encoding.UTF8, "application/json");
        var problem = await ProblemResponse.ReadAsync(
            await client.PostAsync(Path, content, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }

    [Fact]
    public async Task Post_ValidBody_Returns200WithEnumAsString()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Path, new { name = "Nova X", quantity = 2, kind = "Conflict" }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"kind\":\"Conflict\"", body);
    }
}
