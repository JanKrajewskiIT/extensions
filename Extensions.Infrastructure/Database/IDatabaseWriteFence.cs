using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>A host-owned admission check repeated before and after persistence within one transaction.</summary>
public interface IDatabaseWriteFence
{
	/// <summary>Ordinal lock/check ordering key.</summary>
	string Key { get; }

	/// <summary>Whether the fence protects this local context.</summary>
	bool Protects(DbContext context);

	/// <summary>Rejects a cancelled, expired or superseded writer.</summary>
	ValueTask RequireAsync(DbContext context, CancellationToken token);
}
