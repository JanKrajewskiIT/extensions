namespace Extensions.Jobs;

public interface IRecurringOccurrence
{
	string Owner { get; }
	string ScheduleId { get; }
	string ScheduleVersion { get; }
	string IdempotencyPrefix { get; }
	DateTimeOffset ScheduledAt { get; }
	Guid LeaseId { get; }
	DateTimeOffset PreparationDeadline { get; }
}
