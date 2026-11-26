using Gearnix.Api.Data.Configurations;
using Gearnix.Api.Modules.Identity.Entities;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.Data;

/// <summary>
/// The single EF Core context shared by every module (Workflow.md §5.3).
/// Model-wide conventions live here, so every module's entities follow them automatically.
/// It also holds the Identity tables and the Data Protection keys (Workflow.md §6.5, §11.5).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>Keys that encrypt auth cookies; stored here so redeploys do not sign users out.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // PascalCase in C#, snake_case in PostgreSQL (Workflow.md §6).
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own model first; the renames below build on it.
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Configure(modelBuilder);

        // Trigram matching for Vietnamese product search (Workflow.md §10).
        modelBuilder.HasPostgresExtension("pg_trgm");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is always numeric(18,2) (Workflow.md §6, Rule.md §4).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
