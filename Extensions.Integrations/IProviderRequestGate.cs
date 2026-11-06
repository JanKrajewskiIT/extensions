namespace Extensions.Integrations;

public interface IProviderRequestGate
{
	ValueTask<IAsyncDisposable> EnterAsync(string provider, IProviderRequestPolicy policy, CancellationToken cancellationToken = default);

	ValueTask DeferAsync(string provider, IProviderRequestPolicy policy, TimeSpan delay, CancellationToken cancellationToken = default);
}
