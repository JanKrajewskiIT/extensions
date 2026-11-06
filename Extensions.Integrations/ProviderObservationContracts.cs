namespace Extensions.Integrations;

public enum ProviderCleanupStage
{
	RenewalStop,
	LeaseRelease,
	CancellationCallback,
	StreamRelease,
}

public sealed record ProviderRequestTiming(string Dependency, string Category, DateTimeOffset StartedAt,
	DateTimeOffset FinishedAt, TimeSpan Elapsed, DateTimeOffset? ServerDate);
