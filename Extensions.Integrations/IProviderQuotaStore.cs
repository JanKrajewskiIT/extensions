namespace Extensions.Integrations;

/// <summary>A single authoritative admission boundary; implementations atomically check policy and update counters/leases.</summary>
public interface IProviderQuotaStore
{
	ValueTask<TimeSpan> ReserveAsync(ProviderRequestPolicy policy, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken);

	ValueTask DeferAsync(string partition, TimeSpan delay, CancellationToken cancellationToken);

	ValueTask RenewAsync(string partition, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken);

	ValueTask ReleaseAsync(string partition, Guid leaseId, CancellationToken cancellationToken);
}
