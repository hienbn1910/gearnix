using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.Data;

/// <summary>
/// The single EF Core context shared by every module (Workflow.md §5.3).
/// Model-wide conventions live here, so every module's entities follow them automatically.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // PascalCase in C#, snake_case in PostgreSQL (Workflow.md §6).
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is always numeric(18,2) (Workflow.md §6, Rule.md §4).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
