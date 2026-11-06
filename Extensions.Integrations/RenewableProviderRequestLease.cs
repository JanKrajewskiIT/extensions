namespace Extensions.Integrations;

internal sealed class RenewableProviderRequestLease : IProviderRequestLease
{
	private readonly IProviderQuotaStore store;
	private readonly string partition;
	private readonly Guid leaseId;
	private readonly TimeProvider clock;
	private readonly ProviderGateOptions options;
	private readonly IProviderRequestObserver observer;
	private readonly CancellationTokenSource renewal = new();
	private readonly CancellationTokenSource lost = new();
	private readonly Task heartbeat;
	private int disposed;

	internal RenewableProviderRequestLease(IProviderQuotaStore store, string partition, Guid leaseId,
		TimeProvider clock, ProviderGateOptions options, IProviderRequestObserver observer)
	{
		this.store = store;
		this.partition = partition;
		this.leaseId = leaseId;
		this.clock = clock;
		this.options = options;
		this.observer = observer;
		heartbeat = RenewUntilDisposedAsync();
	}

	public CancellationToken LeaseLost => lost.Token;

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
		{
			return;
		}

		try
		{
			await renewal.CancelAsync();
			await heartbeat;
		}
		catch (OperationCanceledException) when (renewal.IsCancellationRequested)
		{
		}
		catch (Exception failure)
		{
			observer.CleanupFailed(ProviderCleanupStage.RenewalStop, failure);
		}
		finally
		{
			renewal.Dispose();
			lost.Dispose();
			using var cleanup = new CancellationTokenSource(options.CleanupTimeout, clock);
			try
			{
				await store.ReleaseAsync(partition, leaseId, cleanup.Token);
			}
			catch (Exception failure)
			{
				observer.CleanupFailed(ProviderCleanupStage.LeaseRelease, failure);
			}
		}
	}

	private async Task RenewUntilDisposedAsync()
	{
		try
		{
			while (!renewal.IsCancellationRequested)
			{
				await Task.Delay(options.LeaseDuration / 3, clock, renewal.Token);
				using var deadline = new CancellationTokenSource(options.LeaseDuration / 3, clock);
				using var attempt = CancellationTokenSource.CreateLinkedTokenSource(renewal.Token, deadline.Token);
				await store.RenewAsync(partition, leaseId, options.LeaseDuration, attempt.Token);
			}
		}
		catch (OperationCanceledException) when (renewal.IsCancellationRequested)
		{
		}
		catch (Exception failure)
		{
			observer.LeaseLost(failure);
			try
			{
				await lost.CancelAsync();
			}
			catch (Exception secondary)
			{
				observer.CleanupFailed(ProviderCleanupStage.CancellationCallback, secondary);
			}
		}
	}
}
