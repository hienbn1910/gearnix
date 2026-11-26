using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.Data.Seeding;
using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Identity;

public sealed class SeedTests(PostgresFixture postgres)
{
    private const string StaffPassword = "staff demo password";
    private const string AdminPassword = "admin demo password";

    [Fact]
    public async Task Seed_DevelopmentTwice_SucceedsAndDemoAccountsCanSignIn()
    {
        await using var factory = DevelopmentFactory(StaffPassword, AdminPassword);

        var first = await SeedCommand.RunAsync(factory.Services, TestContext.Current.CancellationToken);
        var second = await SeedCommand.RunAsync(factory.Services, TestContext.Current.CancellationToken);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal("WarehouseStaff", await SignInRoleAsync(factory, "staff@demo.local", StaffPassword));
        Assert.Equal("Admin", await SignInRoleAsync(factory, "admin@demo.local", AdminPassword));
    }

    [Fact]
    public async Task Seed_DevelopmentWithoutDemoPassword_FailsWithClearMessage()
    {
        await using var factory = DevelopmentFactory(staffPassword: "", AdminPassword);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedCommand.RunAsync(factory.Services, TestContext.Current.CancellationToken));

        Assert.Contains("Seed:StaffPassword", error.Message);
    }

    [Fact]
    public async Task Seed_TestEnvironment_RefusesWithExitCode1()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);

        var exitCode = await SeedCommand.RunAsync(factory.Services, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    private GearnixApiFactory DevelopmentFactory(string staffPassword, string adminPassword) =>
        new(postgres.ConnectionString, "Development", new Dictionary<string, string?>
        {
            ["Seed:StaffPassword"] = staffPassword,
            ["Seed:AdminPassword"] = adminPassword,
        });

    private static async Task<string?> SignInRoleAsync(GearnixApiFactory factory, string email, string password)
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeBody>(TestContext.Current.CancellationToken);
        return me?.Role;
    }

    private sealed record MeBody(string Role);
}
