namespace Extensions.Integrations;

/// <summary>Adapts a host execution budget without introducing a scheduler dependency.</summary>
public interface IProviderRequestExecutor
{
	CancellationToken Token { get; }

	TimeSpan MaximumRequestDuration { get; }

	Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken);
}
