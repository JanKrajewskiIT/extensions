namespace Extensions.Jobs;

public interface IJobExecution<out TJob> where TJob : class
{
	TJob Job { get; }
	ValueTask SaveCheckpointAsync(string checkpointPayload, CancellationToken cancellationToken = default);
}
