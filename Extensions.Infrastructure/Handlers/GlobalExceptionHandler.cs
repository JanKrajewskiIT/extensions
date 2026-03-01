using Extensions.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Extensions.Infrastructure.Handlers;

internal sealed class GlobalExceptionHandler(
	IHostEnvironment env,
	IProblemDetailsService problemDetailsService,
	ILogger<GlobalExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		if (exception is ValidationException)
		{
			return false;
		}

		logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

		var statusCode = GetStatusCode(exception);

		var problemDetails = new ProblemDetails
		{
			Title = "An Error Occurred",
			Status = statusCode,
			Instance = httpContext.Request.Path,
			Detail = env.IsDevelopment() ? exception.Message : "An unexpected error occurred. Please try again later.",
		};

		if (env.IsDevelopment())
		{
			problemDetails.Extensions.Add("stackTrace", exception.StackTrace);
			problemDetails.Extensions.Add("innerMessage", exception.InnerException?.Message);
		}

		httpContext.Response.StatusCode = statusCode;

		await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = problemDetails,
			Exception = exception
		});

		return true;
	}

	private static int GetStatusCode(Exception exception) => exception switch
	{
		DataInconsistencyException => StatusCodes.Status422UnprocessableEntity,
		ArgumentNullException => StatusCodes.Status422UnprocessableEntity,
		ArgumentException => StatusCodes.Status422UnprocessableEntity,
		// InvalidOperationException signals a domain conflict (e.g. duplicate, wrong state)
		InvalidOperationException => StatusCodes.Status409Conflict,
		KeyNotFoundException => StatusCodes.Status404NotFound,
		TimeoutException => StatusCodes.Status408RequestTimeout,
		NotImplementedException => StatusCodes.Status501NotImplemented,
		// UnauthorizedAccessException is a permissions failure, not an authentication failure
		UnauthorizedAccessException => StatusCodes.Status403Forbidden,
		HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } ex
			=> (int)ex.StatusCode,
		_ => StatusCodes.Status500InternalServerError
	};
}
