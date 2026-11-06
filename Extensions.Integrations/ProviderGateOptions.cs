namespace Extensions.Integrations;

public sealed record ProviderGateOptions
{
	public TimeSpan MaximumQueueWait { get; init; } = TimeSpan.FromSeconds(5);

	public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(3);

	public TimeSpan CleanupTimeout { get; init; } = TimeSpan.FromSeconds(10);

	public void Validate()
	{
		if (MaximumQueueWait <= TimeSpan.Zero || MaximumQueueWait > TimeSpan.FromMinutes(1) ||
			LeaseDuration < TimeSpan.FromSeconds(3) || LeaseDuration > TimeSpan.FromMinutes(5) ||
			CleanupTimeout <= TimeSpan.Zero || CleanupTimeout > TimeSpan.FromSeconds(30))
		{
			throw new ArgumentException("Provider admission requires bounded queue, lease and cleanup durations.");
		}
	}
}
