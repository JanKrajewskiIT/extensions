using System.Collections.Immutable;

namespace Extensions.Infrastructure.Http;

/// <summary>Stable RFC problem JSON with machine codes and request correlation, without exception details.</summary>
public sealed record HttpProblemDetails(string Type, string Title, int Status, string Code, string TraceId,
	string CorrelationId, DateTimeOffset Timestamp, bool Retryable, ImmutableArray<HttpValidationError> ValidationErrors,
	string Detail = "Use the error code and correlation ID to diagnose the request.");
