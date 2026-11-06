namespace Extensions.Integrations;

public sealed record ProviderQuotaState(long Minute, int MinuteCount, long Day, int DayCount,
	int ActiveRequests, DateTimeOffset DeferredUntil);

public sealed record ProviderQuotaDecision(ProviderQuotaState State, TimeSpan Delay);
