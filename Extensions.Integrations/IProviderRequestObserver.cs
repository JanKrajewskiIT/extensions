namespace Extensions.Integrations;

/// <summary>Host-owned, non-throwing diagnostics; no response bodies or credentials are supplied.</summary>
public interface IProviderRequestObserver
{
	void Completed(ProviderRequestTiming timing);

	void LeaseLost(Exception failure);

	void CleanupFailed(ProviderCleanupStage stage, Exception failure);
}
