using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Local immutable publication engine over owner-supplied queries, mappings and admission.</summary>
public sealed class ImmutableDocumentRegistry<TRow>(DbContext context, Func<TRow, RegistryDocumentStamp> describe,
	Func<RegistryDocumentStamp, TRow> create, Func<string, string> hashPayload) where TRow : class
{
	private readonly DbContext storage = context ?? throw new ArgumentNullException(nameof(context));
	private readonly Func<TRow, RegistryDocumentStamp> describeRow = describe ?? throw new ArgumentNullException(nameof(describe));
	private readonly Func<RegistryDocumentStamp, TRow> createRow = create ?? throw new ArgumentNullException(nameof(create));
	private readonly Func<string, string> fingerprint = hashPayload ?? throw new ArgumentNullException(nameof(hashPayload));

	/// <summary>Stages a new version and its host effects; an identical existing version emits no second effect.</summary>
	public async ValueTask StageAsync(RegistryDocumentStamp document, string key, IQueryable<TRow> identity,
		Func<CancellationToken, ValueTask> admit, Func<CancellationToken, ValueTask> stageEffects, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(identity);
		ArgumentNullException.ThrowIfNull(admit);
		ArgumentNullException.ThrowIfNull(stageEffects);
		if (storage.Database.CurrentTransaction is null)
		{
			throw new InvalidOperationException("Registry staging requires an explicit transaction owned by the caller.");
		}

		await admit(token);
		await storage.LockAsync(key, token);
		var row = await identity.AsNoTracking().SingleOrDefaultAsync(token);
		if (row is not null)
		{
			var existing = describeRow(row);
			if (existing.Owner != document.Owner)
			{
				throw new UnauthorizedAccessException("The registry identity is unavailable.");
			}

			if (existing.ContentHash != document.ContentHash || fingerprint(existing.Payload) != fingerprint(document.Payload) ||
				existing.ScopeKey != document.ScopeKey || existing.AvailableAt != document.AvailableAt ||
				existing.ResourceId != document.ResourceId || existing.RevokedAt is not null)
			{
				throw new InvalidOperationException("A published version cannot be overwritten with different content.");
			}

			return;
		}

		storage.Add(createRow(document));
		await stageEffects(token);
		await storage.SaveChangesAsync(token);
	}

	/// <summary>Reads a pinned local row, then invokes host admission and an optional frozen-view replacement.</summary>
	public async ValueTask<TRow?> ReadAsync(IQueryable<TRow> selection, string expectedHash,
		Func<CancellationToken, ValueTask> admit, Func<TRow, TRow?>? replace, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(selection);
		ArgumentNullException.ThrowIfNull(admit);
		var row = await selection.AsNoTracking().SingleOrDefaultAsync(token);
		if (row is null)
		{
			return null;
		}

		if (describeRow(row).ContentHash != expectedHash)
		{
			throw new InvalidDataException("The stored version does not match the pinned content hash.");
		}

		await admit(token);
		return replace is null ? row : replace(row);
	}
}
