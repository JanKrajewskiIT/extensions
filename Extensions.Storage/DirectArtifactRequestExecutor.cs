namespace Extensions.Storage;

internal sealed class DirectArtifactRequestExecutor : IArtifactRequestExecutor
{
	public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return request(cancellationToken);
	}
}
