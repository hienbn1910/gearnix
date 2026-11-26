using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Gearnix.Api.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container shared by the whole integration test run.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
