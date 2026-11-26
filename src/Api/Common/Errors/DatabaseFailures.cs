using Npgsql;

namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Decides whether an exception means "the database is temporarily unavailable" (→ 503, Workflow.md §11.3).
/// </summary>
public static class DatabaseFailures
{
    /// <summary>
    /// True when PostgreSQL cannot be reached or cannot serve right now:
    /// a connection-level Npgsql failure, or a server error of class 08 (connection), 53 (insufficient resources)
    /// or 57P01–57P03 (shutting down / starting up).
    /// Deadlocks (40P01) and serialization failures (40001) are NOT included: the code must retry those
    /// (Workflow.md §11.3), so one that escapes is a bug (500). EF Core may wrap the Npgsql exception,
    /// so the whole inner-exception chain is checked.
    /// </summary>
    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgres:
                    return IsUnavailableSqlState(postgres.SqlState);
                case NpgsqlException:
                    // Not a server error: the connection itself failed (refused, network, pool timeout).
                    return true;
            }
        }

        return false;
    }

    private static bool IsUnavailableSqlState(string sqlState) =>
        sqlState.StartsWith("08", StringComparison.Ordinal)
        || sqlState.StartsWith("53", StringComparison.Ordinal)
        || sqlState is "57P01" or "57P02" or "57P03";
}
