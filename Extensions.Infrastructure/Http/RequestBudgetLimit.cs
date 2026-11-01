namespace Extensions.Infrastructure.Http;

/// <summary>Endpoint-specific request deadline metadata.</summary>
public sealed record RequestBudgetLimit
{
	/// <summary>The upper bound shared by endpoint metadata and configured default validation.</summary>
	public static TimeSpan MaximumDuration { get; } = TimeSpan.FromDays(1);

	/// <summary>Creates a positive, timer-compatible deadline.</summary>
	public RequestBudgetLimit(TimeSpan duration)
	{
		if (duration <= TimeSpan.Zero || duration > MaximumDuration)
		{
			throw new ArgumentOutOfRangeException(nameof(duration));
		}

		Duration = duration;
	}

	/// <summary>The deadline measured from entry into the request middleware.</summary>
	public TimeSpan Duration { get; }
}
