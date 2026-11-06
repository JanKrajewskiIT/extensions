using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Local scan and compare-before-revocation engine; the host supplies admission, classification and facts.</summary>
public sealed class ConfigurationDriftGuard<TRow>(DbContext context) where TRow : class
{
	private readonly DbContext storage = context ?? throw new ArgumentNullException(nameof(context));

	/// <summary>Reads one explicitly ordered page; filtering and cursor identity remain owner supplied.</summary>
	public async ValueTask<ConfigurationScanPage<TRow, TCursor>> ReadPageAsync<TCursor>(IOrderedQueryable<TRow> query,
		int pageSize, Func<TRow, TCursor> cursor, CancellationToken token) where TCursor : class
	{
		ArgumentNullException.ThrowIfNull(query);
		ArgumentNullException.ThrowIfNull(cursor);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
		var rows = await query.AsNoTracking().Take(pageSize).ToListAsync(token);
		return new ConfigurationScanPage<TRow, TCursor>(rows, rows.Count == pageSize ? cursor(rows[^1]) : null);
	}

	/// <summary>Revokes only the same observed version and commits the owner's fact with its local change.</summary>
	public async ValueTask<string?> InspectAsync<TDocument>(IQueryable<TRow> selection, string lockKey,
		Func<TRow, bool> current, Func<TRow, TDocument> read,
		Func<TDocument, CancellationToken, ValueTask> require, Func<Exception, string?> classify,
		Func<TRow, TDocument, string, CancellationToken, ValueTask> revoke, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(selection);
		ArgumentNullException.ThrowIfNull(current);
		ArgumentNullException.ThrowIfNull(read);
		ArgumentNullException.ThrowIfNull(require);
		ArgumentNullException.ThrowIfNull(classify);
		ArgumentNullException.ThrowIfNull(revoke);
		var source = await selection.AsNoTracking().SingleOrDefaultAsync(token);
		if (source is null || !current(source))
		{
			return null;
		}

		var document = read(source);
		string reason;
		try
		{
			await require(document, token);
			return null;
		}
		catch (Exception failure) when (classify(failure) is { } code)
		{
			reason = code;
		}

		await using var transaction = await DatabaseTransactions.BeginAsync(storage, token);
		await storage.LockAsync(lockKey, token);
		var row = await selection.SingleAsync(token);
		if (!current(row))
		{
			return null;
		}

		await revoke(row, document, reason, token);
		await storage.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
		return reason;
	}
}
