namespace Extensions.Infrastructure.Database;

/// <summary>Owner admission for an explicit reference or document payload, independent of a global lineage graph.</summary>
public interface IRegistryDependencyGuard<TReference>
{
	/// <summary>Admits an exact pinned reference in the caller's owner context.</summary>
	ValueTask RequireAsync(string owner, TReference reference, CancellationToken token);

	/// <summary>Admits dependencies described by an owner-supplied payload.</summary>
	ValueTask RequirePayloadAsync(string owner, string payload, CancellationToken token);
}
