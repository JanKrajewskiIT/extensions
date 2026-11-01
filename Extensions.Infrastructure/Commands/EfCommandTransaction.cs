using System.Data;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Commands;

internal sealed class EfCommandTransaction<TContext>(TContext callerContext, IServiceScopeFactory scopes,
    Func<IServiceProvider, TContext> resolveContext, ILogger<EfCommandTransaction<TContext>> logger) where TContext : DbContext
{
    public async Task<TResponse> ExecuteAsync<TResponse>(
        Func<IServiceProvider, TContext, CancellationToken, Task<TResponse>> action, CancellationToken token)
    {
        if (!callerContext.Database.IsNpgsql())
        {
            throw new InvalidOperationException("The command pipeline requires a PostgreSQL context.");
        }

        if (callerContext.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null ||
            callerContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("A pipeline command must own its transaction and cannot join pending caller writes.");
        }

        var strategy = callerContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async cancellation =>
        {
            cancellation.ThrowIfCancellationRequested();
            var scope = scopes.CreateAsyncScope();
            IDbContextTransaction? transaction = null;
            Exception? failure = null;
            try
            {
                var context = resolveContext(scope.ServiceProvider);
                if (ReferenceEquals(context, callerContext))
                {
                    throw new InvalidOperationException("Command contexts must be scoped or transient, not shared between attempts.");
                }

                transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellation);
                var result = await action(scope.ServiceProvider, context, cancellation);
                if (!ReferenceEquals(context.Database.CurrentTransaction, transaction))
                {
                    throw new InvalidOperationException("A handler must not commit or replace the pipeline transaction.");
                }

                await context.SaveChangesAsync(cancellation);
                await transaction.CommitAsync(cancellation);
                return result;
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                await DisposeAttemptAsync(transaction, scope, failure);
            }
        }, token);
    }

    private async ValueTask DisposeAttemptAsync(IDbContextTransaction? transaction, AsyncServiceScope scope, Exception? primary)
    {
        Exception? cleanupFailure = null;
        if (transaction is not null)
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch (Exception error)
            {
                cleanupFailure = error;
                logger.LogWarning("Command transaction cleanup failed with {FailureType}", error.GetType().Name);
            }
        }

        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception error)
        {
            cleanupFailure ??= error;
            logger.LogWarning("Command scope cleanup failed with {FailureType}", error.GetType().Name);
        }

        if (primary is null && cleanupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
