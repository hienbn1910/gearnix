using Gearnix.Api.Data;
using Gearnix.Api.Modules.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Gearnix.Api.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container shared by the whole integration test run.
/// Migrations and the seed are applied once here, the way deployment applies them, never at application startup.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        // The same seeder the seed command runs; in the Test environment it creates the roles only.
        // Note: tests share this database, and SeedTests also adds staff@demo.local and admin@demo.local,
        // so never assert on total user or role-member counts.
        await using var factory = new GearnixApiFactory(ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
