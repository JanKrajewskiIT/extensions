namespace Extensions.Jobs;

public record SharedWorkBudgetPolicy<TReference>(
	TReference Reference,
	int MaximumConcurrency,
	int ReservedCriticalConcurrency,
	long DailySlotMilliseconds,
	long ReservedCriticalSlotMilliseconds,
	TimeSpan MaximumAttemptDuration,
	TimeSpan MaximumOperationDuration,
	int MaximumOperationAttempts);
