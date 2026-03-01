using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Extensions.Infrastructure.Database;

public static class MigrationExtensions
{
	public static async Task MigrateDatabase<TContext>(this IServiceProvider services, CancellationToken cancellationToken)
		where TContext : DbContext
	{
		await using var scope = services.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

		var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

		if (pendingMigrations.Any())
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
