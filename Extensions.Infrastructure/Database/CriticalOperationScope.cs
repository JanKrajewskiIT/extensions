namespace Extensions.Infrastructure.Database;

/// <summary>Typed ambient operation metadata; entering a scope does not authorize its owner or actor.</summary>
public sealed class CriticalOperationScope<TOperation> : IDisposable where TOperation : class
{
	private static readonly AsyncLocal<CriticalOperationScope<TOperation>?> ambient = new();
	private readonly CriticalOperationScope<TOperation>? previous;

	/// <summary>Enters an operation synchronously in the caller's execution context.</summary>
	public CriticalOperationScope(TOperation operation)
	{
		ArgumentNullException.ThrowIfNull(operation);
		Operation = operation;
		previous = ambient.Value;
		ambient.Value = this;
	}

	/// <summary>The current operation for this owner's contract type.</summary>
	public static TOperation? Current => ambient.Value?.Operation;

	/// <summary>The owned operation.</summary>
	public TOperation Operation { get; }

	/// <inheritdoc />
	public void Dispose()
	{
		if (ambient.Value != this)
		{
			throw new InvalidOperationException("Critical operations must close in ownership order.");
		}

		ambient.Value = previous;
	}
}
