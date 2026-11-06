namespace Extensions.Integrations;

/// <summary>Resolved admission input; the authoritative store verifies the host-owned policy hash.</summary>
public sealed record ProviderRequestPolicy(string Provider, string? QuotaPartition, string PolicyHash,
	ProviderRequestLimits Limits, ProviderCapacityClass Capacity)
{
	public string Partition => QuotaPartition ?? Provider;

	public static ProviderRequestPolicy Resolve(IProviderRequestPolicy policy, string provider)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentException.ThrowIfNullOrWhiteSpace(provider);
		var resolved = policy.Resolve(provider);
		resolved.Validate();
		return resolved;
	}

	public void Validate()
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
		ArgumentException.ThrowIfNullOrWhiteSpace(Partition);
		ArgumentException.ThrowIfNullOrWhiteSpace(PolicyHash);
		ArgumentNullException.ThrowIfNull(Limits);
		if (Provider.Length > 256 || Partition.Length > 256 || PolicyHash.Length > 256 || !Enum.IsDefined(Capacity))
		{
			throw new ArgumentException("Provider admission requires bounded identities and a defined capacity class.");
		}

		Limits.Validate();
		if (Capacity == ProviderCapacityClass.Reserved &&
			(Limits.ReservedConcurrency == 0 || Limits.ReservedMinuteRequests == 0 || Limits.ReservedDailyRequests == 0))
		{
			throw new ArgumentException("Reserved admission requires explicit capacity in each quota.");
		}
	}
}
