using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Handlers;

/// <summary>
/// Exception handler for FluentValidation <see cref="FluentValidation.ValidationException"/>.
/// Returns HTTP 422 with a <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails"/> body
/// that groups errors by property name.
/// </summary>
internal sealed class ValidationExceptionHandler(
	IProblemDetailsService problemDetailsService,
	ILogger<ValidationExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		if (exception is not ValidationException validationException)
		{
			return false;
		}

		logger.LogWarning(exception, "Validation exception occurred: {Message}", exception.Message);

		var validationErrors = validationException.Errors
			.GroupBy(e => e.PropertyName, e => e.ErrorMessage)
			.ToDictionary(e => e.Key, e => e.ToArray());

		var problemDetails = new ValidationProblemDetails
		{
			Title = "Validation Error Occurred",
			Status = StatusCodes.Status422UnprocessableEntity,
			Instance = httpContext.Request.Path,
			Detail = validationException.Message,
			Errors = validationErrors,
		};

		httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

		await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = problemDetails,
			Exception = validationException
		});

		return true;
	}
}
