using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory, in the Test environment, against the given database connection string.
/// </summary>
public sealed class GearnixApiFactory(string? connectionString) : WebApplicationFactory<Program>
{
    /// <summary>A port on the local machine where no PostgreSQL listens.</summary>
    public const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Username=nobody;Password=nothing;Database=none";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        if (connectionString is not null)
        {
            builder.UseSetting("Database:ConnectionString", connectionString);
        }
    }
}
