namespace Extensions.Integrations;

public sealed record ProviderRequestLimits(int RequestsPerMinute, int RequestsPerUtcDay, int MaximumConcurrency,
	int ReservedConcurrency = 0, int ReservedMinuteRequests = 0, int ReservedDailyRequests = 0)
{
	public void Validate()
	{
		if (RequestsPerMinute is < 1 or > 100000 || RequestsPerUtcDay is < 1 or > 10000000 ||
			MaximumConcurrency is < 1 or > 1024 || ReservedConcurrency < 0 || ReservedConcurrency >= MaximumConcurrency ||
			ReservedMinuteRequests < 0 || ReservedMinuteRequests >= RequestsPerMinute ||
			ReservedDailyRequests < 0 || ReservedDailyRequests >= RequestsPerUtcDay)
		{
			throw new ArgumentException("Provider quotas require positive bounded limits and smaller nonnegative reservations.");
		}
	}
}
