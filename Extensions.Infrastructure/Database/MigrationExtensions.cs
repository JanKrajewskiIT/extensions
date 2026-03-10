using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Helpers for running EF Core migrations at application startup.
/// </summary>
public static class MigrationExtensions
{
	/// <summary>
	/// Applies any pending EF Core migrations for <typeparamref name="TContext"/>.
	/// After migration, reloads Npgsql type mappings so newly registered enums/composites are available immediately.
	/// </summary>
	public static async Task MigrateDatabase<TContext>(this IServiceProvider services, CancellationToken cancellationToken)
		where TContext : DbContext
	{
		await using var scope = services.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

		var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

		if (pendingMigrations.Count > 0)
		{
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
}
