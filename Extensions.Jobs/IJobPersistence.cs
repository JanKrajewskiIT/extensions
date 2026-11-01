namespace Extensions.Jobs;

public interface IJobPersistence<TJob> where TJob : class
{
	ValueTask<TJob?> FindAsync(Guid jobId, string scopeKey, CancellationToken token);
	ValueTask<bool> SaveAsync(TJob job, long expectedVersion, CancellationToken token);
}
