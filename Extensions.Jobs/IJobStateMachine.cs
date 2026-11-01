namespace Extensions.Jobs;

public interface IJobStateMachine<out TJob> where TJob : class
{
	JobDetails Details { get; }
	TJob Snapshot();
	void Acquire(Guid attemptId, string owner, DateTimeOffset now, TimeSpan duration);
	void RecoverExpiredLease(DateTimeOffset now, DateTimeOffset retryAt);
	void Expire(DateTimeOffset now);
	void Renew(DateTimeOffset now, TimeSpan duration);
	void SaveCheckpoint(string checkpointPayload, DateTimeOffset now);
	void Complete(JobResult result, DateTimeOffset now);
	void AcknowledgeCancellation(DateTimeOffset now);
	void DeferForCapacity(string code, DateTimeOffset now, DateTimeOffset retryAt);
	void Fail(string code, JobFailureKind kind, DateTimeOffset now, DateTimeOffset? retryAt);
}
