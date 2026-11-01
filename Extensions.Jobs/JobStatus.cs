namespace Extensions.Jobs;

public enum JobStatus
{
	Pending,
	Running,
	RetryScheduled,
	Completed,
	Failed,
	Cancelled,
}
