using Gearnix.Api.Data;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Gearnix.Api.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container shared by the whole integration test run.
/// Migrations are applied once here, the way deployment applies them, never at application startup.
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
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
