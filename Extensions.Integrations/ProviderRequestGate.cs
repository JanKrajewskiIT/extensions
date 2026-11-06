using Extensions.Domain.Exceptions;

namespace Extensions.Integrations;

/// <summary>Explicit process-local admission; it must not replace a shared-account authority.</summary>
public sealed class ProviderRequestGate : IProviderRequestGate
{
	private const int MaximumPartitions = 256;
	private readonly object sync = new();
	private readonly Dictionary<string, Partition> partitions = new(StringComparer.Ordinal);
	private readonly TimeProvider clock;
	private readonly ProviderGateOptions options;

	public ProviderRequestGate(TimeProvider clock, ProviderGateOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(clock);
		this.clock = clock;
		this.options = options ?? new ProviderGateOptions();
		this.options.Validate();
	}

	public async ValueTask<IAsyncDisposable> EnterAsync(string provider, IProviderRequestPolicy policy,
		CancellationToken cancellationToken = default)
	{
		var resolved = ProviderRequestPolicy.Resolve(policy, provider);
		var partition = GetPartition(resolved);
		var started = clock.GetTimestamp();
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var remaining = options.MaximumQueueWait - clock.GetElapsedTime(started);
			if (remaining <= TimeSpan.Zero)
			{
				throw new DependencyFailureException(ProviderFailureCodes.CapacityExhausted, true, TimeSpan.FromSeconds(1));
			}

			ProviderQuotaDecision decision;
			lock (sync)
			{
				decision = ProviderQuotaWindow.Reserve(partition.State, resolved.Limits, resolved.Capacity, clock.GetUtcNow());
				partition.State = decision.State;
			}

			if (decision.Delay <= TimeSpan.Zero)
			{
				return new Lease(() => Release(partition));
			}

			if (decision.Delay >= remaining)
			{
				throw new DependencyFailureException(ProviderFailureCodes.CapacityExhausted, true, decision.Delay);
			}

			await Task.Delay(decision.Delay, clock, cancellationToken);
		}
	}

	public ValueTask DeferAsync(string provider, IProviderRequestPolicy policy, TimeSpan delay,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var resolved = ProviderRequestPolicy.Resolve(policy, provider);
		if (delay <= TimeSpan.Zero || delay > TimeSpan.FromDays(1))
		{
			throw new ArgumentOutOfRangeException(nameof(delay));
		}

		var partition = GetPartition(resolved);
		lock (sync)
		{
			var until = clock.GetUtcNow() + delay;
			if (until > partition.State.DeferredUntil)
			{
				partition.State = partition.State with { DeferredUntil = until };
			}
		}

		return ValueTask.CompletedTask;
	}

	private Partition GetPartition(ProviderRequestPolicy policy)
	{
		lock (sync)
		{
			if (partitions.TryGetValue(policy.Partition, out var existing))
			{
				if (existing.Hash != policy.PolicyHash || existing.Limits != policy.Limits)
				{
					throw new InvalidOperationException("A local quota policy changed during the host lifetime.");
				}

				return existing;
			}

			if (partitions.Count >= MaximumPartitions)
			{
				throw new InvalidOperationException("The local provider partition budget is exhausted.");
			}

			var created = new Partition(policy.PolicyHash, policy.Limits);
			partitions.Add(policy.Partition, created);
			return created;
		}
	}

	private void Release(Partition partition)
	{
		lock (sync)
		{
			partition.State = partition.State with { ActiveRequests = checked(partition.State.ActiveRequests - 1) };
		}
	}

	private sealed class Partition(string hash, ProviderRequestLimits limits)
	{
		internal string Hash { get; } = hash;
		internal ProviderRequestLimits Limits { get; } = limits;
		internal ProviderQuotaState State { get; set; } = new(0, 0, 0, 0, 0, DateTimeOffset.UnixEpoch);
	}

	private sealed class Lease(Action release) : IAsyncDisposable
	{
		private Action? owned = release;

		public ValueTask DisposeAsync()
		{
			Interlocked.Exchange(ref owned, null)?.Invoke();
			return ValueTask.CompletedTask;
		}
	}
}
