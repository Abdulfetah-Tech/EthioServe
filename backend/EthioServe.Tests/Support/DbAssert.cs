using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Tests.Support;

public static class DbAssert
{
    /// <summary>Runs the action and returns the PostgreSQL error it must cause. Fails if the database accepts it.</summary>
    public static async Task<PostgresException> RejectedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            return pg;
        }
        catch (PostgresException pg)
        {
            return pg;
        }

        throw new Xunit.Sdk.XunitException("Expected PostgreSQL to reject the statement, but it succeeded.");
    }

    public static void Violates(PostgresException error, string sqlState, string constraintName)
    {
        Assert.Equal(sqlState, error.SqlState);
        Assert.Equal(constraintName, error.ConstraintName);
    }
}