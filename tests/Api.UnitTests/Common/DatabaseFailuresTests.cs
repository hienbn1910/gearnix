using System.Net.Sockets;

using Gearnix.Api.Common.Errors;

using Npgsql;

namespace Gearnix.Api.UnitTests.Common;

public sealed class DatabaseFailuresTests
{
    [Fact]
    public void IsUnavailable_ConnectionRefused_ReturnsTrue()
    {
        var exception = new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused));

        Assert.True(DatabaseFailures.IsUnavailable(exception));
    }

    [Fact]
    public void IsUnavailable_WrappedConnectionFailure_ReturnsTrue()
    {
        var exception = new InvalidOperationException(
            "An error occurred using the connection",
            new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused)));

        Assert.True(DatabaseFailures.IsUnavailable(exception));
    }

    [Theory]
    [InlineData("08006")] // connection_failure
    [InlineData("53300")] // too_many_connections
    [InlineData("57P01")] // admin_shutdown
    [InlineData("57P03")] // cannot_connect_now (server starting up)
    public void IsUnavailable_ServerUnavailableSqlState_ReturnsTrue(string sqlState)
    {
        Assert.True(DatabaseFailures.IsUnavailable(Postgres(sqlState)));
    }

    [Theory]
    [InlineData("40P01")] // deadlock_detected: must be retried by the code (Workflow.md §11.3), else a bug → 500
    [InlineData("40001")] // serialization_failure: same
    [InlineData("23505")] // unique_violation: a programming error → 500
    public void IsUnavailable_OtherSqlState_ReturnsFalse(string sqlState)
    {
        Assert.False(DatabaseFailures.IsUnavailable(Postgres(sqlState)));
    }

    [Fact]
    public void IsUnavailable_NonDatabaseException_ReturnsFalse()
    {
        Assert.False(DatabaseFailures.IsUnavailable(new InvalidOperationException("bug")));
    }

    private static PostgresException Postgres(string sqlState) =>
        new("message", "ERROR", "ERROR", sqlState);
}
