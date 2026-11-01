using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Extensions.Infrastructure.Database;

/// <summary>A separate nonpooled connection owns a session lock across local transaction commits.</summary>
public static class DatabaseOperationLease
{
	/// <summary>Acquires an independently owned session lease.</summary>
	public static async ValueTask<IAsyncDisposable> AcquireAsync(DbContext context, string key, CancellationToken token) =>
		await AcquireConnectionAsync(context, key, token);

	/// <summary>Returns the owned connection; disposal releases the session lock, never a pooled session.</summary>
	public static ValueTask<NpgsqlConnection> AcquireConnectionAsync(DbContext context, string key, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(context);
		return AcquireConnectionAsync(context.Database.GetConnectionString()!, key, token);
	}

	/// <summary>Acquires from a host-owned connection string without logging its contents.</summary>
	public static async ValueTask<NpgsqlConnection> AcquireConnectionAsync(string connectionString, string key, CancellationToken token)
	{
		key = DatabaseLockKeys.FromText(key);
		var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
		var connection = new NpgsqlConnection(settings.ConnectionString);
		try
		{
			await connection.OpenAsync(token);
			// Session advisory locks intentionally outlive individual EF transactions.
			await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", connection);
			command.Parameters.AddWithValue("key", key);
			await command.ExecuteNonQueryAsync(token);
			return connection;
		}
		catch (Exception primary)
		{
			try
			{
				await connection.DisposeAsync();
			}
			catch (Exception cleanup)
			{
				primary.Data["Extensions.Database.LeaseCleanupFailure"] = cleanup.GetType().FullName;
			}

			throw;
		}
	}
}
