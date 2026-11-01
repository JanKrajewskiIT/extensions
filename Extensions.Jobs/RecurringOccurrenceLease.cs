namespace Extensions.Jobs;

public record RecurringOccurrenceLease<TSchedule>(TSchedule Definition, string Owner, DateTimeOffset ScheduledAt,
	Guid LeaseId, DateTimeOffset PreparationDeadline);
