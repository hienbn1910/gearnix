using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Security;

public sealed class AuthorizationTests
{
    [Fact]
    public async Task SignedInEndpoint_Anonymous_Returns401ProblemNotRedirect()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateApiClient();

        var problem = await ProblemResponse.ReadAsync(
            await client.GetAsync("/api/test-auth/signed-in", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("unauthorized", problem.Code);
    }

    [Theory]
    [InlineData("customer", "Customer", HttpStatusCode.OK)]
    [InlineData("customer", "Admin", HttpStatusCode.Forbidden)]
    [InlineData("staff", "Customer", HttpStatusCode.Forbidden)]
    [InlineData("staff", "WarehouseStaff", HttpStatusCode.OK)]
    [InlineData("staff", "Admin", HttpStatusCode.OK)]
    [InlineData("warehouse-staff", "WarehouseStaff", HttpStatusCode.OK)]
    [InlineData("warehouse-staff", "Admin", HttpStatusCode.Forbidden)] // separation of duties (Workflow.md §3)
    [InlineData("admin", "Admin", HttpStatusCode.OK)]
    [InlineData("admin", "WarehouseStaff", HttpStatusCode.Forbidden)]
    public async Task Policy_Role_ReturnsExpectedStatus(string action, string role, HttpStatusCode expected)
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateApiClientAs(Guid.CreateVersion7(), role);

        var response = await client.GetAsync($"/api/test-auth/{action}", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Policy_WrongRole_Returns403ProblemWithForbiddenCode()
    {
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateApiClientAs(Guid.CreateVersion7(), "Customer");

        var problem = await ProblemResponse.ReadAsync(
            await client.GetAsync("/api/test-auth/admin", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("forbidden", problem.Code);
    }

    [Fact]
    public async Task CurrentUser_SignedIn_ReturnsIdAndRoleFromPrincipal()
    {
        var userId = Guid.CreateVersion7();
        await using var factory = new GearnixApiFactory(GearnixApiFactory.UnreachableConnectionString);
        using var client = factory.CreateApiClientAs(userId, "WarehouseStaff");

        var body = await client.GetFromJsonAsync<CurrentUserBody>(
            "/api/test-auth/signed-in", TestContext.Current.CancellationToken);

        Assert.Equal(new CurrentUserBody(userId, "WarehouseStaff"), body);
    }

    private sealed record CurrentUserBody(Guid Id, string Role);
}
