using Extensions.Domain.Exceptions;

namespace Extensions.Integrations;

public sealed class DistributedProviderRequestGate : IProviderRequestGate
{
	private readonly IProviderQuotaStore store;
	private readonly TimeProvider clock;
	private readonly ProviderGateOptions options;
	private readonly IProviderRequestObserver observer;

	public DistributedProviderRequestGate(IProviderQuotaStore store, TimeProvider clock,
		ProviderGateOptions? options = null, IProviderRequestObserver? observer = null)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(clock);
		this.store = store;
		this.clock = clock;
		this.options = options ?? new ProviderGateOptions();
		this.options.Validate();
		this.observer = observer ?? ProviderTraceObserver.Instance;
	}

	public async ValueTask<IAsyncDisposable> EnterAsync(string provider, IProviderRequestPolicy policy,
		CancellationToken cancellationToken = default)
	{
		var resolved = ProviderRequestPolicy.Resolve(policy, provider);
		var lease = Guid.NewGuid();
		var started = clock.GetTimestamp();
		using var timeout = new CancellationTokenSource(options.MaximumQueueWait, clock);
		using var queue = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
		try
		{
			while (true)
			{
				queue.Token.ThrowIfCancellationRequested();
				var delay = await store.ReserveAsync(resolved, lease, options.LeaseDuration, queue.Token);
				if (delay <= TimeSpan.Zero)
				{
					return new RenewableProviderRequestLease(store, resolved.Partition, lease, clock, options, observer);
				}

				var remaining = options.MaximumQueueWait - clock.GetElapsedTime(started);
				if (delay >= remaining)
				{
					throw new DependencyFailureException(ProviderFailureCodes.CapacityExhausted, true, delay);
				}

				await Task.Delay(delay < TimeSpan.FromSeconds(1) ? delay : TimeSpan.FromSeconds(1), clock, queue.Token);
			}
		}
		catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
		{
			throw new DependencyFailureException(ProviderFailureCodes.CapacityExhausted, true, TimeSpan.FromSeconds(1), failure);
		}
	}

	public ValueTask DeferAsync(string provider, IProviderRequestPolicy policy, TimeSpan delay,
		CancellationToken cancellationToken = default)
	{
		var resolved = ProviderRequestPolicy.Resolve(policy, provider);
		if (delay <= TimeSpan.Zero || delay > TimeSpan.FromDays(1))
		{
			throw new ArgumentOutOfRangeException(nameof(delay));
		}

		return store.DeferAsync(resolved.Partition, delay, cancellationToken);
	}
}
