namespace Extensions.Integrations;

public interface IProviderRequestLease : IAsyncDisposable
{
	CancellationToken LeaseLost { get; }
}
