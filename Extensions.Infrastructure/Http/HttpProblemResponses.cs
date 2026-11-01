using System.Collections.Immutable;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure.Http;

/// <summary>One non-disclosing error shape for exceptions, status pages and application results.</summary>
public static class HttpProblemResponses
{
	/// <summary>Maximum exported field errors per response.</summary>
	public const int MaximumValidationErrors = 1000;

	/// <summary>Common API status set; hosts can publish additional statuses explicitly.</summary>
	public static ImmutableArray<int> Statuses { get; } = [400, 401, 403, 404, 409, 412, 413, 415, 422, 428, 429, 500, 503];

	/// <summary>Builds the public contract. Automatic retry is advertised only for transient reads.</summary>
	public static HttpProblemDetails Create(HttpContext context, HttpFailure failure)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(failure);
		if (failure.Status is < 400 or > 599)
		{
			throw new ArgumentOutOfRangeException(nameof(failure));
		}

		var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) ||
			context.GetEndpoint()?.Metadata.GetMetadata<ReadOnlyOperation>() is not null;
		ImmutableArray<HttpValidationError> errors = failure.ValidationErrors.IsDefault ? [] : failure.ValidationErrors;
		var correlation = HttpCorrelation.GetOrCreate(context);
		var clock = context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
		return new("about:blank", "The operation could not be completed.", failure.Status,
			SafeCode(failure.Code, "operation.failed"), context.TraceIdentifier, correlation.ToString("D"), clock.GetUtcNow(),
			read && failure.AllowReadRetry && failure.Status is (429 or 503),
			[.. errors.Take(MaximumValidationErrors).Select(error => new HttpValidationError(DefaultHttpExceptionMapper.SafePath(error.Path),
				SafeCode(error.Code, "request.invalid")))]);
	}

	/// <summary>Creates a JSON result and applies a bounded Retry-After when supplied by the host.</summary>
	public static IResult Problem(HttpContext context, HttpFailure failure)
	{
		var problem = Create(context, failure);
		if (failure.RetryAfter is { } delay)
		{
			SetRetryAfter(context, delay);
		}

		return Results.Json(problem, contentType: "application/problem+json", statusCode: failure.Status);
	}

	/// <summary>Applies one bounded Retry-After policy to rate limiting and dependency errors.</summary>
	public static void SetRetryAfter(HttpContext context, TimeSpan delay)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (!context.Response.HasStarted)
		{
			context.Response.Headers.RetryAfter = Math.Clamp(Math.Ceiling(delay.TotalSeconds), 1, 86400).ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>Default machine code for an HTTP status.</summary>
	public static string Code(int status) => status switch
	{
		400 => "request.invalid",
		401 => "authentication.required",
		403 => "resource.forbidden",
		404 => "resource.not_found",
		408 => "request.timeout",
		409 => "operation.conflict",
		412 => "resource.precondition_failed",
		413 => "request.too_large",
		415 => "request.media_type_unsupported",
		422 => "request.unprocessable",
		428 => "resource.precondition_required",
		429 => "request.rate_limited",
		501 => "operation.not_implemented",
		503 => "dependency.unavailable",
		_ => "operation.failed",
	};

	/// <summary>Accepts bounded machine identifiers; the mapper must supply trusted codes, never exception messages.</summary>
	public static string SafeCode(string? code, string fallback) =>
		!string.IsNullOrEmpty(code) && code.Length <= 160 && code.All(value => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-' or ':')
			? code : fallback;
}
