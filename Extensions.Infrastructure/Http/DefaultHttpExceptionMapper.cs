using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using Extensions.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Default status policy; a host can replace it without replacing the response or logging pipeline.</summary>
public sealed class DefaultHttpExceptionMapper : IHttpExceptionMapper
{
	/// <inheritdoc />
	public HttpFailure? Map(HttpContext context, Exception exception) => MapDefault(context, exception);

	/// <summary>Maps shared and BCL failures, retaining the original Extensions status distinctions.</summary>
	public static HttpFailure? MapDefault(HttpContext context, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(exception);
		return exception switch
		{
			OperationCanceledException when context.RequestAborted.IsCancellationRequested => null,
			ValidationException validation => new(422, "request.invalid", Validation(validation)),
			DependencyFailureException dependency => new(503, dependency.Code, RetryAfter: dependency.RetryAfter, AllowReadRetry: dependency.Retryable),
			ICodedException coded => new(coded.Kind switch
			{
				FailureKind.Validation or FailureKind.DataInconsistency => 422,
				FailureKind.NotFound => 404,
				FailureKind.Dependency => 503,
				FailureKind.Conflict or FailureKind.BusinessRule => 409,
				_ => 500,
			}, coded.Code),
			BadHttpRequestException request => new(request.StatusCode, HttpProblemResponses.Code(request.StatusCode)),
			JsonException json => new(400, "request.invalid", [new(SafePath(json.Path), "json.invalid")]),
			ArgumentException => new(422, "request.invalid"),
			InvalidOperationException => new(409, "operation.conflict"),
			KeyNotFoundException => new(404, "resource.not_found"),
			TimeoutException => new(408, "request.timeout"),
			NotImplementedException => new(501, "operation.not_implemented"),
			UnauthorizedAccessException => new(403, "resource.forbidden"),
			HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } http =>
				new((int)http.StatusCode.Value, HttpProblemResponses.Code((int)http.StatusCode.Value)),
			_ => new(500, "operation.failed"),
		};
	}

	/// <summary>Exports only bounded paths and machine error codes, never validator messages or attempted values.</summary>
	public static ImmutableArray<HttpValidationError> Validation(ValidationException exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return [.. exception.Errors.Take(HttpProblemResponses.MaximumValidationErrors).Select(error => new HttpValidationError(SafePath(error.PropertyName),
			HttpProblemResponses.SafeCode(error.ErrorCode, "request.invalid")))];
	}

	/// <summary>Bounds a field path and rejects control characters before exposing it.</summary>
	public static string SafePath(string? path) =>
		string.IsNullOrEmpty(path) || path.Length > 512 || path.Any(char.IsControl) ? "$" : path;
}
