namespace Extensions.Jobs;

public enum RecurringOccurrenceOutcome
{
	WaitingForDependencies,
	Dispatched,
	Skipped,
}
