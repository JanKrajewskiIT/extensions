namespace Extensions.Integrations;

public sealed class ProviderStreamingResponse(HttpResponseMessage response, Stream stream,
	IProviderRequestObserver? observer = null) : IAsyncDisposable
{
	private readonly IProviderRequestObserver observations = observer ?? ProviderTraceObserver.Instance;
	private IAsyncDisposable? lease;
	private CancellationTokenSource? cancellation;
	private CancellationTokenRegistration registration;
	private int disposed;
	private int responseDisposed;

	public Stream Stream { get; } = stream;

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
		{
			return;
		}

		registration.Dispose();
		try
		{
			DisposeResponse();
		}
		finally
		{
			cancellation?.Dispose();
			if (Interlocked.Exchange(ref lease, null) is { } owned)
			{
				try
				{
					await owned.DisposeAsync();
				}
				catch (Exception failure)
				{
					observations.CleanupFailed(ProviderCleanupStage.LeaseRelease, failure);
				}
			}
		}
	}

	internal void OwnLease(IAsyncDisposable owned, CancellationTokenSource requestCancellation)
	{
		lease = owned;
		cancellation = requestCancellation;
		registration = requestCancellation.Token.Register(DisposeResponse);
	}

	private void DisposeResponse()
	{
		if (Interlocked.Exchange(ref responseDisposed, 1) != 0)
		{
			return;
		}

		try
		{
			response.Dispose();
		}
		catch (Exception failure)
		{
			observations.CleanupFailed(ProviderCleanupStage.StreamRelease, failure);
		}
	}
}
