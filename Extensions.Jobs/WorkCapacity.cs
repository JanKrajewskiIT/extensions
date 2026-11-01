namespace Extensions.Jobs;

public static class WorkCapacity
{
	public static void Validate<TReference>(SharedWorkBudgetPolicy<TReference> policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(policy.Reference);
		if (policy.MaximumConcurrency is < 2 or > 256 || policy.ReservedCriticalConcurrency < 1 || policy.ReservedCriticalConcurrency >= policy.MaximumConcurrency ||
			policy.MaximumAttemptDuration < TimeSpan.FromSeconds(1) || policy.MaximumAttemptDuration > TimeSpan.FromHours(1) ||
			policy.MaximumOperationDuration < policy.MaximumAttemptDuration || policy.MaximumOperationDuration > TimeSpan.FromDays(31) ||
			policy.MaximumOperationAttempts is < 1 or > 1000000 || policy.DailySlotMilliseconds < (long)policy.MaximumAttemptDuration.TotalMilliseconds ||
			policy.DailySlotMilliseconds > 86400000L * policy.MaximumConcurrency || policy.ReservedCriticalSlotMilliseconds < (long)policy.MaximumAttemptDuration.TotalMilliseconds ||
			policy.ReservedCriticalSlotMilliseconds >= policy.DailySlotMilliseconds ||
			policy.DailySlotMilliseconds - policy.ReservedCriticalSlotMilliseconds < (long)policy.MaximumAttemptDuration.TotalMilliseconds)
		{
			throw new ArgumentException("Shared work budgets require bounded concurrency, reserved critical capacity, slot milliseconds and operation limits.");
		}
	}

	public static void Require<TReference>(SharedWorkBudgetPolicy<TReference> policy, DateOnly day, long used, long usedBackground,
		IReadOnlyCollection<CapacityReservation> active, bool critical, long reserved)
	{
		var reservedToday = active.Where(value => DateOnly.FromDateTime(value.StartedAt.UtcDateTime) == day).Sum(value => value.ReservedMilliseconds);
		var background = active.Where(value => !value.Protected).ToArray();
		var backgroundReserved = background.Where(value => DateOnly.FromDateTime(value.StartedAt.UtcDateTime) == day).Sum(value => value.ReservedMilliseconds);
		if (active.Count >= policy.MaximumConcurrency || used + reservedToday > policy.DailySlotMilliseconds - reserved ||
			!critical && (background.Length >= policy.MaximumConcurrency - policy.ReservedCriticalConcurrency ||
				usedBackground + backgroundReserved > policy.DailySlotMilliseconds - policy.ReservedCriticalSlotMilliseconds - reserved))
		{
			throw new WorkAdmissionDeferredException("budget.shared_capacity_exhausted", TimeSpan.FromSeconds(5));
		}
	}

	public static DateTimeOffset Deadline<TReference>(SharedWorkBudgetPolicy<TReference> policy, DateTimeOffset now,
		DateTimeOffset requested, DateTimeOffset operation, DateTimeOffset approvedUntil)
	{
		var deadline = Earlier(Earlier(Earlier(requested, operation), now + policy.MaximumAttemptDuration), approvedUntil);
		return Earlier(deadline, new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero));
	}

	public static long Charge(IWorkAllocation allocation, WorkOutcome outcome, DateTimeOffset now)
	{
		var used = Math.Clamp(checked((long)Math.Ceiling((now - allocation.StartedAt).TotalMilliseconds)), 0, allocation.ReservedMilliseconds);
		return outcome == WorkOutcome.Abandoned || now >= allocation.Deadline ? allocation.ReservedMilliseconds : used;
	}

	public static DateTimeOffset Earlier(DateTimeOffset left, DateTimeOffset right)
	{
		return left < right ? left : right;
	}
}
