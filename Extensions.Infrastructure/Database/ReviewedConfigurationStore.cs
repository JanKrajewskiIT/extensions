using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Transactional revision and proposal-idempotency engine; the owner supplies lifecycle state and effects.</summary>
public sealed class ReviewedConfigurationStore<TRow>(DbContext context) where TRow : class
{
	private readonly DbContext storage = context ?? throw new ArgumentNullException(nameof(context));

	/// <summary>Changes a local configuration state and commits its host-owned facts atomically.</summary>
	public async ValueTask<TState> ChangeAsync<TState>(TState next, long expectedRevision, string lockKey,
		IQueryable<TRow> selection, Func<TRow, TState> read, Func<TState, long> revision,
		Func<TState, string> proposalHash, Func<TState, string> stateHash,
		Func<TState, TRow?, CancellationToken, ValueTask> stage, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(selection);
		ArgumentNullException.ThrowIfNull(read);
		ArgumentNullException.ThrowIfNull(revision);
		ArgumentNullException.ThrowIfNull(proposalHash);
		ArgumentNullException.ThrowIfNull(stateHash);
		ArgumentNullException.ThrowIfNull(stage);
		await using var transaction = await storage.Database.BeginTransactionAsync(token);
		await storage.LockAsync(lockKey, token);
		var row = await selection.SingleOrDefaultAsync(token);
		if (row is not null && expectedRevision == 0)
		{
			var existing = read(row);
			if (proposalHash(existing) != stateHash(next))
			{
				throw new InvalidOperationException("The configuration command was reused with different content.");
			}

			return existing;
		}

		if ((row is null ? 0 : revision(read(row))) != expectedRevision)
		{
			throw new DbUpdateConcurrencyException("The configuration change revision changed.");
		}

		await stage(next, row, token);
		await storage.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
		storage.ChangeTracker.Clear();
		return next;
	}
}
