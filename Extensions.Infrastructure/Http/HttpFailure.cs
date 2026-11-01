using System.Collections.Immutable;

namespace Extensions.Infrastructure.Http;

/// <summary>Trusted application mapping consumed by the common problem response writer.</summary>
public sealed record HttpFailure(int Status, string Code, ImmutableArray<HttpValidationError> ValidationErrors = default,
	TimeSpan? RetryAfter = null, bool AllowReadRetry = true);
