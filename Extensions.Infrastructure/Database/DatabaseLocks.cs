using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>PostgreSQL transaction locks shared by write and command pipelines.</summary>
public static class DatabaseLocks
{
	/// <summary>Acquires the exact text key until the caller-owned transaction ends.</summary>
	public static async ValueTask LockAsync(this DbContext context, string key, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		key = DatabaseLockKeys.FromText(key);
		if (context.Database.CurrentTransaction is null)
		{
			throw new InvalidOperationException("An advisory lock requires an explicit transaction.");
		}

		// PostgreSQL transaction advisory locks have no equivalent EF LINQ operation.
		await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
	}
}
