using System.ComponentModel.DataAnnotations;

using Npgsql;

namespace Gearnix.Api.Data;

/// <summary>
/// Database connection settings, from the "Database" configuration section.
/// Pool limits follow the capacity plan in Workflow.md §11.1.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = "";

    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>How long a request waits for a free pooled connection before failing.</summary>
    [Range(1, 60)]
    public int PoolTimeoutSeconds { get; set; } = 15;

    /// <summary>The configured connection string with the pool limits applied.</summary>
    public string BuildConnectionString() =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            MaxPoolSize = MaxPoolSize,
            Timeout = PoolTimeoutSeconds,
        }.ConnectionString;
}
