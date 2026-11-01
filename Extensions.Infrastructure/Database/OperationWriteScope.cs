using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Nested ambient write-fence registration with strict ownership-order disposal.</summary>
public sealed class OperationWriteScope : IDisposable
{
	private static readonly AsyncLocal<OperationWriteScope?> current = new();
	private readonly OperationWriteScope? parent;
	private readonly IDatabaseWriteFence fence;

	/// <summary>Enters a fence synchronously in the caller's execution context.</summary>
	public OperationWriteScope(IDatabaseWriteFence fence)
	{
		ArgumentNullException.ThrowIfNull(fence);
		this.fence = fence;
		parent = current.Value;
		current.Value = this;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (current.Value != this)
		{
			throw new InvalidOperationException("Operation write scopes must be disposed in ownership order.");
		}

		current.Value = parent;
	}

	/// <summary>Snapshots only applicable fences, retaining stable ordinal ordering.</summary>
	public static IDatabaseWriteFence[] For(DbContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var fences = new List<IDatabaseWriteFence>();
		for (var scope = current.Value; scope is not null; scope = scope.parent)
		{
			if (scope.fence.Protects(context))
			{
				fences.Add(scope.fence);
			}
		}

		return [.. fences.OrderBy(value => value.Key, StringComparer.Ordinal)];
	}
}
