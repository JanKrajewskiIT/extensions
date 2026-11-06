using System.Diagnostics;

namespace Extensions.Integrations;

internal sealed class ProviderTraceObserver : IProviderRequestObserver
{
	internal static ProviderTraceObserver Instance { get; } = new();

	public void Completed(ProviderRequestTiming timing)
	{
	}

	public void LeaseLost(Exception failure)
	{
		Trace.TraceError("Provider quota lease was lost ({0}).", failure.GetType().Name);
	}

	public void CleanupFailed(ProviderCleanupStage stage, Exception failure)
	{
		Trace.TraceWarning("Provider cleanup failed at {0} ({1}); the primary result is retained.", stage, failure.GetType().Name);
	}
}
