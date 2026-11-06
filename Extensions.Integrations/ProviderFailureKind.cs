namespace Extensions.Integrations;

public enum ProviderFailureKind
{
	None,
	Transient,
	AccessDenied,
	QuotaExhausted,
	HistoryUnavailable,
	RequestRejected,
}
