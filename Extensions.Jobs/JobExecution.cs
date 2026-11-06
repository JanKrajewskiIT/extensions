namespace Extensions.Jobs;

public sealed class JobExecution<TJob>(TJob initial, ScopedJobOperation<IJobPersistence<TJob>> persistence,
	IJobCodec<TJob> codec, TimeProvider clock, TimeSpan leaseDuration, CancellationTokenSource cancellation)
	: IJobExecution<TJob>, IDisposable where TJob : class
{
	private const int MaximumFinishAttempts = 3;

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
		ArgumentNullException.ThrowIfNull(change);
		await gate.WaitAsync(token);
		try
		{
			await persistence.RunAsync(async (store, operation) =>
			{
				var job = await ReadCurrentAsync(store, operation);
				var version = job.Details.Version;
				change(job);
				var snapshot = job.Snapshot();
				if (!await store.SaveAsync(snapshot, version, operation))
				{
					await cancellation.CancelAsync();
					throw new InvalidOperationException("The job state changed concurrently.");
				}

				Job = snapshot;
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

	internal async ValueTask FinishAsync(Action<IJobStateMachine<TJob>> finish, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(finish);
		await gate.WaitAsync(token);
		try
		{
			for (var attempt = 0; attempt < MaximumFinishAttempts; attempt++)
			{
				var saved = await persistence.RunAsync(async (store, operation) =>
				{
					var job = await ReadCurrentAsync(store, operation);
					var version = job.Details.Version;
					if (job.Details.CancellationRequested)
					{
						job.AcknowledgeCancellation(clock.GetUtcNow());
					}
					else
					{
						finish(job);
					}

					var snapshot = job.Snapshot();
					if (!await store.SaveAsync(snapshot, version, operation))
					{
						return false;
					}

					Job = snapshot;
					return true;
				}, token);
				if (saved)
				{
					return;
				}
			}

			await cancellation.CancelAsync();
			throw new InvalidOperationException("The job state changed concurrently during finalization.");
		}
		finally
		{
			gate.Release();
		}
	}

	private async ValueTask<IJobStateMachine<TJob>> ReadCurrentAsync(IJobPersistence<TJob> store, CancellationToken token)
	{
		var previous = Details;
		var current = await store.FindAsync(previous.Definition.Id, previous.Definition.ScopeKey, token)
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

		return codec.Restore(current);
	}
}
