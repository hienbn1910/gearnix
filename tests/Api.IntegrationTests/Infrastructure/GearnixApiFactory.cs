using FluentValidation;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against the given database connection string, in the Test environment unless another
/// environment is given.
/// </summary>
public sealed class GearnixApiFactory(string? connectionString, string environment = "Test")
    : WebApplicationFactory<Program>
{
    /// <summary>A port on the local machine where no PostgreSQL listens.</summary>
    public const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Username=nobody;Password=nothing;Database=none";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // Always set the value, so a Database__ConnectionString environment variable can never leak into a test.
        builder.UseSetting("Database:ConnectionString", connectionString ?? "");

        // Adds the test-only controllers and validators of this assembly (e.g. TestErrorsController).
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(GearnixApiFactory).Assembly);
            services.AddValidatorsFromAssemblyContaining<GearnixApiFactory>();
        });
    }

    /// <summary>
    /// A client that behaves like the frontend: it sends the CSRF header on every request (Workflow.md §11.5).
    /// Use <c>CreateClient()</c> only to test what happens without it.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return client;
    }
}
