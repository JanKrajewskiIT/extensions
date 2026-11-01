namespace Extensions.Jobs;

public interface IJobWorker<TJob> where TJob : class
{
	ValueTask<TJob?> ClaimAsync(JobRunnerOptions options, CancellationToken token);
	IDisposable EnterScope(TJob job, string workerId);
	ValueTask<JobResult> ExecuteAsync(IJobExecution<TJob> execution, CancellationToken token);
	JobFailure Classify(Exception failure, int attempts);
}
