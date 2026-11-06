using System.Net;
using Extensions.Domain.Exceptions;

namespace Extensions.Integrations;

public sealed class ProviderHttpTransport(HttpClient client, IProviderRequestGate? requestGate = null,
	string? provider = null, IProviderRequestPolicy? rateLimitPolicy = null, TimeProvider? timeProvider = null,
	Func<CancellationToken, ValueTask>? beforeSend = null, IProviderRequestExecutor? executor = null,
	IProviderRequestObserver? observer = null, string dependencyCategory = "provider")
{
	private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
	private readonly IProviderRequestExecutor requestExecutor = executor ?? DirectProviderRequestExecutor.Instance;
	private readonly IProviderRequestObserver observations = observer ?? ProviderTraceObserver.Instance;

	public ProviderHttpTransport WithClient(HttpClient replacement)
	{
		return new ProviderHttpTransport(replacement, requestGate, provider, rateLimitPolicy, clock, beforeSend,
			requestExecutor, observations, dependencyCategory);
	}

	public async ValueTask<ProviderStreamingResponse> OpenStreamAsync(HttpRequestMessage request,
		string provider, IProviderRequestPolicy rateLimitPolicy,
		CancellationToken cancellationToken = default)
	{
		if (requestGate is null)
		{
			throw new InvalidOperationException("Provider request limiting is not registered for this transport.");
		}

		return await OpenLeasedStreamAsync(request, provider, rateLimitPolicy, cancellationToken);
	}

	public async ValueTask<ProviderResponse> SendAsync(HttpRequestMessage request, int maximumResponseBytes,
		string provider, IProviderRequestPolicy rateLimitPolicy,
		CancellationToken cancellationToken = default)
	{
		if (requestGate is null)
		{
			throw new InvalidOperationException("Provider request limiting is not registered for this transport.");
		}

		await using var lease = await requestGate.EnterAsync(provider, rateLimitPolicy, cancellationToken);
		using var requestCancellation = RequestCancellation(lease, cancellationToken);
		return await SendCoreAsync(request, maximumResponseBytes, provider, rateLimitPolicy, requestCancellation.Token);
	}

	public async ValueTask<ProviderStreamingResponse> OpenStreamAsync(HttpRequestMessage request,
		CancellationToken cancellationToken = default)
	{
		if (requestGate is not null && provider is not null && rateLimitPolicy is not null)
		{
			return await OpenLeasedStreamAsync(request, provider, rateLimitPolicy, cancellationToken);
		}

		return await OpenStreamCoreAsync(request, null, null, cancellationToken);
	}

	public async ValueTask<ProviderResponse> SendAsync(HttpRequestMessage request, int maximumResponseBytes,
		CancellationToken cancellationToken = default)
	{
		if (requestGate is not null && provider is not null && rateLimitPolicy is not null)
		{
			await using var lease = await requestGate.EnterAsync(provider, rateLimitPolicy, cancellationToken);
			using var requestCancellation = RequestCancellation(lease, cancellationToken);
			return await SendCoreAsync(request, maximumResponseBytes, provider, rateLimitPolicy, requestCancellation.Token);
		}

		return await SendCoreAsync(request, maximumResponseBytes, null, null, cancellationToken);
	}

	private CancellationTokenSource RequestCancellation(IAsyncDisposable lease, CancellationToken token) =>
		CancellationTokenSource.CreateLinkedTokenSource(token, requestExecutor.Token,
			lease is IProviderRequestLease owned ? owned.LeaseLost : CancellationToken.None);

	private TimeSpan RetryDelay(HttpResponseMessage response)
	{
		var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
		if (response.Headers.RetryAfter?.Date is { } until)
		{
			delay = until - clock.GetUtcNow();
		}

		return delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay;
	}

	private async Task<HttpResponseMessage> SendHeadersAsync(HttpRequestMessage request, string? dependency, CancellationToken token)
	{
		var at = clock.GetUtcNow();
		var started = clock.GetTimestamp();
		HttpResponseMessage? response = null;
		try
		{
			var budgetToken = requestExecutor.Token;
			using var deadline = new CancellationTokenSource(RequestDuration(), clock);
			using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, budgetToken, deadline.Token);
			try
			{
				if (beforeSend is not null)
				{
					await beforeSend(cancellation.Token);
				}

				response = await requestExecutor.SendAsync(client, request, cancellation.Token);
				return response;
			}
			catch (OperationCanceledException failure) when (!token.IsCancellationRequested &&
				!budgetToken.IsCancellationRequested && deadline.IsCancellationRequested)
			{
				throw new TimeoutException("The provider headers exceeded their request deadline.", failure);
			}
		}
		finally
		{
			observations.Completed(new ProviderRequestTiming(dependency ?? "unidentified", dependencyCategory,
				at, clock.GetUtcNow(), clock.GetElapsedTime(started), response?.Headers.Date));
		}
	}

	private async ValueTask<ProviderStreamingResponse> OpenLeasedStreamAsync(HttpRequestMessage request, string provider,
		IProviderRequestPolicy policy, CancellationToken token)
	{
		var lease = await requestGate!.EnterAsync(provider, policy, token);
		var cancellation = RequestCancellation(lease, token);
		try
		{
			var response = await OpenStreamCoreAsync(request, provider, policy, cancellation.Token);
			response.OwnLease(lease, cancellation);
			return response;
		}
		catch
		{
			cancellation.Dispose();
			try
			{
				await lease.DisposeAsync();
			}
			catch (Exception secondary)
			{
				observations.CleanupFailed(ProviderCleanupStage.StreamRelease, secondary);
			}

			throw;
		}
	}

	private async ValueTask<ProviderStreamingResponse> OpenStreamCoreAsync(HttpRequestMessage request,
		string? provider, IProviderRequestPolicy? rateLimitPolicy,
		CancellationToken cancellationToken)
	{
		var response = await SendHeadersAsync(request, provider, cancellationToken);
		try
		{
			if (!response.IsSuccessStatusCode)
			{
				await DeferOnRateLimitAsync(response, provider, rateLimitPolicy, cancellationToken);
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{
					throw new DependencyFailureException(ProviderFailureCodes.RateLimited, true, RetryDelay(response));
				}

				throw ProviderFailures.Exception(response.StatusCode);
			}

			return new ProviderStreamingResponse(response, await response.Content.ReadAsStreamAsync(cancellationToken), observations);
		}
		catch
		{
			response.Dispose();
			throw;
		}
	}

	private async ValueTask<ProviderResponse> SendCoreAsync(HttpRequestMessage request, int maximumResponseBytes,
		string? provider, IProviderRequestPolicy? rateLimitPolicy,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBytes);
		var duration = RequestDuration();
		var budgetToken = requestExecutor.Token;
		using var deadline = new CancellationTokenSource(duration, clock);
		using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budgetToken, deadline.Token);
		try
		{
			return await ReadResponseAsync(request, maximumResponseBytes, provider, rateLimitPolicy, operation.Token);
		}
		catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && !budgetToken.IsCancellationRequested &&
			(deadline.IsCancellationRequested || failure.InnerException is TimeoutException))
		{
			throw new TimeoutException("The provider response exceeded its request deadline.", failure);
		}
	}

	private async ValueTask<ProviderResponse> ReadResponseAsync(HttpRequestMessage request, int maximumResponseBytes,
		string? provider, IProviderRequestPolicy? rateLimitPolicy,
		CancellationToken cancellationToken)
	{
		using var response = await SendHeadersAsync(request, provider, cancellationToken);
		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			var retry = RetryDelay(response);
			await DeferAsync(provider, rateLimitPolicy, retry, cancellationToken);
			throw new DependencyFailureException(ProviderFailureCodes.RateLimited, true, retry);
		}

		if (response.Content.Headers.ContentLength > maximumResponseBytes)
		{
			throw new InvalidDataException("Provider response exceeds the configured limit.");
		}

		await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var bytes = new MemoryStream();
		var buffer = new byte[16384];
		int read;
		while ((read = await content.ReadAsync(buffer, cancellationToken)) != 0)
		{
			if (bytes.Length + read > maximumResponseBytes)
			{
				throw new InvalidDataException("Provider response exceeds the configured limit.");
			}

			await bytes.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return new ProviderResponse(response.StatusCode, bytes.ToArray());
	}

	private async ValueTask DeferOnRateLimitAsync(HttpResponseMessage response, string? provider,
		IProviderRequestPolicy? policy, CancellationToken cancellationToken)
	{
		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			await DeferAsync(provider, policy, RetryDelay(response), cancellationToken);
		}
	}

	private async ValueTask DeferAsync(string? provider, IProviderRequestPolicy? policy, TimeSpan delay, CancellationToken cancellationToken)
	{
		if (requestGate is null || provider is null || policy is null || delay <= TimeSpan.Zero)
		{
			return;
		}

		try
		{
			await requestGate.DeferAsync(provider, policy, delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception failure)
		{
			throw new DependencyFailureException(ProviderFailureCodes.RateLimited, true, delay, failure);
		}
	}

	private TimeSpan RequestDuration()
	{
		var maximum = requestExecutor.MaximumRequestDuration;
		if (maximum <= TimeSpan.Zero || maximum == Timeout.InfiniteTimeSpan)
		{
			throw new InvalidOperationException("A provider executor must supply a positive finite request duration.");
		}

		return client.Timeout == Timeout.InfiniteTimeSpan || client.Timeout > maximum ? maximum : client.Timeout;
	}
}
