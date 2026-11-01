namespace Extensions.Storage;

/// <summary>Host hook for dependency budgets, cancellation and telemetry without a Jobs dependency.</summary>
public interface IArtifactRequestExecutor
{
	/// <summary>Runs a single SDK attempt; it must not blindly repeat an uncertain immutable write.</summary>
	Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> request, CancellationToken cancellationToken);
}
