using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Extensions.Infrastructure.Database;

/// <summary>Enforces execution-strategy compatibility for caller-managed transaction units.</summary>
public static class DatabaseTransactions
{
    /// <summary>Rejects an independently retrying strategy before a helper begins its transaction.</summary>
    public static Task<IDbContextTransaction> BeginAsync(DbContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireCompatibleExecutionStrategy(context);
        return context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>Checks the effective strategy before opening or joining a transaction.</summary>
    public static void RequireCompatibleExecutionStrategy(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException(
                "Shared transaction helpers require an execution strategy that does not independently retry. " +
                "Register the context with PostgresRetryPolicy.NoRetries, or execute the complete unit through " +
                "an outer execution strategy that restores state and owns all transaction attempts.");
        }
    }
}
