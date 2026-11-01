namespace Extensions.Jobs;

public sealed record JobDetails(JobDefinitionInfo Definition, JobStatus Status, int Attempts, LeaseToken? Lease,
	DateTimeOffset? LeaseUntil, DateTimeOffset NextAttemptAt, bool CancellationRequested, long Fence, long Version);
