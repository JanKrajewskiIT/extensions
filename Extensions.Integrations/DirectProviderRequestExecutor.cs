namespace Extensions.Integrations;

public sealed class DirectProviderRequestExecutor : IProviderRequestExecutor
{
	public static DirectProviderRequestExecutor Instance { get; } = new();

	public CancellationToken Token => CancellationToken.None;

	public TimeSpan MaximumRequestDuration { get; } = TimeSpan.FromMinutes(5);

	public Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
	}
}
