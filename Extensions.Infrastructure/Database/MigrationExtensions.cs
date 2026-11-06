using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Helpers for running EF Core migrations at application startup.
/// </summary>
public static class MigrationExtensions
{
    private const int MaximumConnectionAttempts = 5;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Applies any pending EF Core migrations for <typeparamref name="TContext"/>.
    /// Retries on connection failures to handle Aspire port-proxy startup delays.
    /// After migration, reloads Npgsql type mappings so newly registered enums/composites are available immediately.
    /// </summary>
    public static Task MigrateDatabase<TContext>(this IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext => services.MigrateDatabaseAsync<TContext>(cancellationToken);

    /// <summary>
    /// Waits for a reachable database, applies pending migrations and refreshes Npgsql mappings.
    /// Caller cancellation is propagated; migration failures are not retried by this helper.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var clock = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        await WaitForDatabaseAsync(dbContext, logger, clock, cancellationToken).ConfigureAwait(false);

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pendingMigrations.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count);

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            await ReloadTypesAsync(dbContext, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitForDatabaseAsync(DbContext dbContext, ILogger logger, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumConnectionAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connected = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            // CanConnectAsync may convert a provider cancellation into false.
            cancellationToken.ThrowIfCancellationRequested();
            if (connected)
            {
                return;
            }

            if (attempt < MaximumConnectionAttempts)
            {
                var delay = InitialDelay * attempt;

                logger.LogWarning(
                    "Database connection attempt {Attempt}/{MaximumAttempts} failed, retrying in {Delay}s...",
                    attempt,
                    MaximumConnectionAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        logger.LogError("Database startup failed ({FailureCode}) after {Attempts} connection attempts",
            DatabaseStartupException.FailureCode, MaximumConnectionAttempts);
        throw new DatabaseStartupException(MaximumConnectionAttempts);
    }

    private static async Task ReloadTypesAsync(DbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            return;
        }

        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        Exception? reloadFailure = null;
        try
        {
            await connection.ReloadTypesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            reloadFailure = failure;
            throw;
        }
        finally
        {
            if (openedHere)
            {
                try
                {
                    await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
                }
                catch (Exception cleanup) when (reloadFailure is not null)
                {
                    logger.LogWarning("Closing the migration connection after a failed type reload also failed ({FailureType})",
                        cleanup.GetType().Name);
                }
            }
        }
    }
}
