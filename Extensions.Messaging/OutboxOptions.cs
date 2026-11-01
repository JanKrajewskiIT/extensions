namespace Extensions.Messaging;

public sealed class OutboxOptions
{
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);
	public int MaximumAttempts { get; set; } = 10;
}
