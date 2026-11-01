namespace Extensions.Jobs;

public sealed record JobCompleted(Guid JobId, JobStatus Status);
