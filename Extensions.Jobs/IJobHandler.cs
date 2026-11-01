namespace Extensions.Jobs;

public interface IJobHandler<in TJob>
{
	string Kind { get; }
	ValueTask<JobResult> ExecuteAsync(TJob execution, CancellationToken cancellationToken = default);
}
