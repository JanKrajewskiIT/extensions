namespace Extensions.Integrations;

public static class ProviderQuotaWindow
{
	public static ProviderQuotaDecision Reserve(ProviderQuotaState state, ProviderRequestLimits limits,
		ProviderCapacityClass capacity, DateTimeOffset now)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(limits);
		limits.Validate();
		if (state.MinuteCount < 0 || state.DayCount < 0 || state.ActiveRequests < 0 || !Enum.IsDefined(capacity))
		{
			throw new ArgumentException("A provider quota requires nonnegative counters and a defined capacity class.");
		}

		var minute = now.ToUnixTimeSeconds() / 60;
		var day = now.ToUnixTimeSeconds() / 86400;
		state = state with
		{
			Minute = minute,
			MinuteCount = state.Minute == minute ? state.MinuteCount : 0,
			Day = day,
			DayCount = state.Day == day ? state.DayCount : 0,
		};
		var shared = capacity == ProviderCapacityClass.Shared;
		var dailyLimit = limits.RequestsPerUtcDay - (shared ? limits.ReservedDailyRequests : 0);
		var minuteLimit = limits.RequestsPerMinute - (shared ? limits.ReservedMinuteRequests : 0);
		var concurrencyLimit = limits.MaximumConcurrency - (shared ? limits.ReservedConcurrency : 0);
		var delay = state.DeferredUntil > now ? state.DeferredUntil - now : state.DayCount >= dailyLimit
			? DateTimeOffset.FromUnixTimeSeconds((day + 1) * 86400) - now : state.MinuteCount >= minuteLimit
			? DateTimeOffset.FromUnixTimeSeconds((minute + 1) * 60) - now : state.ActiveRequests >= concurrencyLimit
			? TimeSpan.FromMilliseconds(100) : TimeSpan.Zero;
		if (delay == TimeSpan.Zero)
		{
			state = state with
			{
				MinuteCount = checked(state.MinuteCount + 1),
				DayCount = checked(state.DayCount + 1),
				ActiveRequests = checked(state.ActiveRequests + 1),
			};
		}

		return new ProviderQuotaDecision(state, delay);
	}
}
