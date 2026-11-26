using Gearnix.Api.Modules.Identity;

namespace Gearnix.Api.Data.Seeding;

/// <summary>
/// The "seed" command-line mode (Workflow.md §13.4): `dotnet run --project src/Api -- seed` seeds the database and
/// exits without starting the web server, so application startup stays free of database work (§11.3).
/// Runs after migrations, in development by hand and as a deploy step (§11.6). Every seeder is idempotent.
/// </summary>
public static class SeedCommand
{
    public const string Argument = "seed";

    /// <summary>The seeder runs only here (Workflow.md §4); Test seeds its own data.</summary>
    private static readonly string[] AllowedEnvironments = ["Development", "Demo", "LoadTest"];

    public static bool IsRequested(string[] args) => args is [Argument];

    /// <returns>The process exit code: 0 on success, 1 when the environment is not allowed.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedCommand));

        if (!AllowedEnvironments.Contains(environment.EnvironmentName))
        {
            logger.LogError(
                "Seeding is not allowed in the {Environment} environment; allowed: {Allowed}",
                environment.EnvironmentName,
                string.Join(", ", AllowedEnvironments));
            return 1;
        }

        await using var scope = services.CreateAsyncScope();

        // Order matters as later tasks add seeders: roles and accounts first, then catalog (W1-08), then stock (W2-03).
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(cancellationToken);

        logger.LogInformation("Seeding completed in the {Environment} environment", environment.EnvironmentName);
        return 0;
    }
}
