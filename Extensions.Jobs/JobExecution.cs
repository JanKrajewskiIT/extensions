namespace Extensions.Jobs;

public sealed class JobExecution<TJob>(TJob initial, ScopedJobOperation<IJobPersistence<TJob>> persistence,
	IJobCodec<TJob> codec, TimeProvider clock, TimeSpan leaseDuration, CancellationTokenSource cancellation)
	: IJobExecution<TJob>, IDisposable where TJob : class
{
	private readonly SemaphoreSlim gate = new(1, 1);

	public TJob Job { get; private set; } = initial;

	public JobDetails Details => codec.Describe(Job);

	public ValueTask SaveCheckpointAsync(string payload, CancellationToken cancellationToken = default)
	{
		return ChangeAsync(job => job.SaveCheckpoint(payload, clock.GetUtcNow()), cancellationToken);
	}

	public ValueTask RenewAsync(CancellationToken token)
	{
		return ChangeAsync(job => job.Renew(clock.GetUtcNow(), leaseDuration), token);
	}

	public async ValueTask ChangeAsync(Action<IJobStateMachine<TJob>> change, CancellationToken token)
	{
		await gate.WaitAsync(token);
		try
		{
			await persistence.RunAsync(async (store, operation) =>
			{
				var previous = Details;
				var current = await store.FindAsync(previous.Definition.Id, previous.Definition.ScopeKey, operation)
					?? throw new InvalidOperationException("The running job disappeared.");
				var details = codec.Describe(current);
				if (details.Lease != previous.Lease || details.Status != JobStatus.Running)
				{
					await cancellation.CancelAsync();
					throw new InvalidOperationException("The job lease was lost.");
				}

				Job = current;
				if (details.CancellationRequested)
				{
					await cancellation.CancelAsync();
				}

				var job = codec.Restore(current);
				change(job);
				if (!await store.SaveAsync(job.Snapshot(), details.Version, operation))
				{
					await cancellation.CancelAsync();
					throw new InvalidOperationException("The job state changed concurrently.");
				}

				Job = job.Snapshot();
			}, token);
		}
		finally
		{
			gate.Release();
		}
	}

	public void Dispose()
	{
		gate.Dispose();
	}
}
