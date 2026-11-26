using FluentValidation;

using Gearnix.Api.Common.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against the given database connection string, in the Test environment unless another
/// environment is given. Extra settings override every other configuration source.
/// </summary>
public sealed class GearnixApiFactory(
    string? connectionString,
    string environment = "Test",
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    private const string TestOrCookieScheme = "TestOrCookie";

    /// <summary>A port on the local machine where no PostgreSQL listens.</summary>
    public const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Username=nobody;Password=nothing;Database=none";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // Added last, so these win over appsettings, user-secrets (loaded in Development) and environment variables.
        // The connection string is always set, so a machine's own value can never leak into a test.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
            {
                ["Database:ConnectionString"] = connectionString ?? "",
            }));

        // Adds the test-only controllers and validators of this assembly (e.g. TestErrorsController).
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(GearnixApiFactory).Assembly);
            services.AddValidatorsFromAssemblyContaining<GearnixApiFactory>();

            // Requests with X-Test-User use the test scheme; all others use the real Identity cookie.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null)
                .AddPolicyScheme(TestOrCookieScheme, null, scheme => scheme.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(TestAuthHandler.HeaderName)
                        ? TestAuthHandler.SchemeName
                        : IdentityConstants.ApplicationScheme);
            services.PostConfigure<AuthenticationOptions>(options => options.DefaultScheme = TestOrCookieScheme);
        });
    }

    /// <summary>
    /// A client that behaves like the frontend: it sends the CSRF header on every request (Workflow.md §11.5).
    /// Use <c>CreateClient()</c> only to test what happens without it.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        // HTTPS, so the Secure auth cookie is stored and sent back like in a browser.
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(CsrfHeaderMiddleware.HeaderName, CsrfHeaderMiddleware.HeaderValue);
        return client;
    }

    /// <summary>A frontend-like client acting as the given user and role through the test authentication scheme.</summary>
    public HttpClient CreateApiClientAs(Guid userId, string role)
    {
        var client = CreateApiClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, $"{userId};{role}");
        return client;
    }
}
