using Extensions.Infrastructure.Http;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Handlers;

internal sealed class ValidationExceptionHandler(IHttpExceptionMapper mapper, ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
	public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken) =>
		exception is ValidationException ? HttpExceptionResponses.TryHandleAsync(context, exception, mapper, logger) : ValueTask.FromResult(false);
}
