using System.Net.Sockets;
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
	private const int MaxRetries = 5;
	private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Applies any pending EF Core migrations for <typeparamref name="TContext"/>.
	/// Retries on connection failures to handle Aspire port-proxy startup delays.
	/// After migration, reloads Npgsql type mappings so newly registered enums/composites are available immediately.
	/// </summary>
	public static async Task MigrateDatabase<TContext>(this IServiceProvider services, CancellationToken cancellationToken)
		where TContext : DbContext
	{
		await using var scope = services.CreateAsyncScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

		await WaitForDatabaseAsync(dbContext, logger, cancellationToken).ConfigureAwait(false);

		var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

		if (pendingMigrations.Count > 0)
		{
			logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count);

			await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

			await using var connection = dbContext.Database.GetDbConnection();

			if (connection is NpgsqlConnection npgsqlConnection)
			{
				if (npgsqlConnection.State != System.Data.ConnectionState.Open)
				{
					await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
				}

				await npgsqlConnection.ReloadTypesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static async Task WaitForDatabaseAsync(DbContext dbContext, ILogger logger, CancellationToken cancellationToken)
	{
		for (var attempt = 1; attempt <= MaxRetries; attempt++)
		{
			try
			{
				await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (NpgsqlException ex) when (ex.InnerException is SocketException && attempt < MaxRetries)
			{
				var delay = InitialDelay * attempt;

				logger.LogWarning(
					"Database connection attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s...",
					attempt,
					MaxRetries,
					delay.TotalSeconds);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
