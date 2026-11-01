namespace Extensions.Infrastructure.Database;

/// <summary>Local owner inspection port; candidate/cursor and admission remain host supplied.</summary>
public interface IConfigurationDriftInspector<TCandidate, TCursor> where TCursor : class
{
	/// <summary>Owner-local store identifier.</summary>
	string Store { get; }

	/// <summary>Reads one bounded candidate page.</summary>
	ValueTask<ConfigurationScanPage<TCandidate, TCursor>> ReadPageAsync(TCursor? after, CancellationToken token);

	/// <summary>Inspects the exact candidate and returns an optional classified revocation reason.</summary>
	ValueTask<string?> InspectAsync(TCandidate candidate, CancellationToken token);
}
