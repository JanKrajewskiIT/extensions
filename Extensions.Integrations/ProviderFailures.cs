using System.Net;
using Extensions.Domain.Exceptions;

namespace Extensions.Integrations;

public static class ProviderFailures
{
	public static ProviderFailureKind Classify(HttpStatusCode status)
	{
		var code = (int)status;
		return code switch
		{
			>= 200 and < 300 => ProviderFailureKind.None,
			401 or 403 => ProviderFailureKind.AccessDenied,
			429 => ProviderFailureKind.QuotaExhausted,
			404 => ProviderFailureKind.HistoryUnavailable,
			408 or >= 500 => ProviderFailureKind.Transient,
			_ => ProviderFailureKind.RequestRejected,
		};
	}

	public static DependencyFailureException Exception(HttpStatusCode status, TimeSpan? retryAfter = null)
	{
		var kind = Classify(status);
		return kind switch
		{
			ProviderFailureKind.Transient => new DependencyFailureException("provider.unavailable", true, retryAfter),
			ProviderFailureKind.QuotaExhausted => new DependencyFailureException(ProviderFailureCodes.RateLimited, true, retryAfter),
			ProviderFailureKind.AccessDenied => new DependencyFailureException("provider.access_denied", false),
			ProviderFailureKind.HistoryUnavailable => new DependencyFailureException("provider.history_unavailable", false),
			ProviderFailureKind.RequestRejected => new DependencyFailureException("provider.request_rejected", false),
			_ => throw new ArgumentException("A successful response has no provider failure.", nameof(status)),
		};
	}
}
