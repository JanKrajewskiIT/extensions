using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Prevents writes after cancellation without knowing a product context or job type.</summary>
public sealed class CancellationWriteFence(string key, CancellationToken token) : IDatabaseWriteFence
{
	/// <inheritdoc />
	public string Key { get; } = DatabaseLockKeys.FromText(key);

	/// <inheritdoc />
	public bool Protects(DbContext context) => true;

	/// <inheritdoc />
	public ValueTask RequireAsync(DbContext context, CancellationToken cancellationToken)
	{
		token.ThrowIfCancellationRequested();
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}
