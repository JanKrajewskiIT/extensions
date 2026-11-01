namespace Extensions.Messaging;

public class KafkaSubscriptionOptions
{
	public const int DefaultMaximumWorkAttempts = 1000;
	public string Name { get; set; } = "";
	public string Topic { get; set; } = "";
	public string Group { get; set; } = "";
	public bool RequiresContinuity { get; set; } = true;
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromSeconds(30);
	public int MaximumAttempts { get; set; } = 3;
	public int MaximumWorkAttempts { get; set; } = DefaultMaximumWorkAttempts;
	public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromMilliseconds(250);
	public TimeSpan RetryMaximumDelay { get; set; } = TimeSpan.FromSeconds(5);
	public string[] MessageTypes { get; set; } = [];
	public TimeSpan ProcessingBudget => TimeSpan.FromTicks(checked(ProcessingTimeout.Ticks * MaximumAttempts));

	public bool HasValidGroup(string prefix) => !string.IsNullOrWhiteSpace(Group) && Group.Length <= 200 &&
		Group.StartsWith(prefix, StringComparison.Ordinal) && Group.All(value => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-');

	public bool HasValidProcessingPolicy() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Topic) &&
		ProcessingTimeout > TimeSpan.Zero && ProcessingTimeout <= TimeSpan.FromMinutes(2) &&
		MaximumAttempts is >= 1 and <= 10 && MaximumWorkAttempts >= MaximumAttempts && MaximumWorkAttempts <= 1000000 &&
		RetryInitialDelay >= TimeSpan.FromMilliseconds(10) && RetryInitialDelay <= RetryMaximumDelay &&
		RetryMaximumDelay <= TimeSpan.FromSeconds(5);
}
