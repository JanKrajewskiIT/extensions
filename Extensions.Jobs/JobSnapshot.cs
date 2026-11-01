namespace Extensions.Jobs;

public record JobSnapshot<TDefinition, TCheckpoint, TArtifact>(TDefinition Definition, JobStatus Status, int Attempts,
	LeaseToken? Lease, DateTimeOffset? LeaseUntil, DateTimeOffset NextAttemptAt, bool CancellationRequested,
	TCheckpoint? Checkpoint, TArtifact? Result, string? FailureCode, long Fence, long Version)
	where TCheckpoint : class where TArtifact : class;
