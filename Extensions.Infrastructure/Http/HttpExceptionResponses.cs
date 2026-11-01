using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Extensions.Infrastructure.Http;

internal static class HttpExceptionResponses
{
	internal static async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
		IHttpExceptionMapper mapper, ILogger logger)
	{
		if (context.Response.HasStarted || exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
		{
			return false;
		}

		var failure = mapper.Map(context, exception);
		if (failure is null)
		{
			return false;
		}

		var response = HttpProblemResponses.Problem(context, failure);
		var correlation = HttpCorrelation.GetOrCreate(context);
		var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
		var level = failure.Status >= 500 || failure.Code == "operation.conflict" ? LogLevel.Error : LogLevel.Warning;
		logger.Log(level, new EventId(1001, "ApiRequestFailed"),
			"API request failed: HTTP {StatusCode}, code {FailureCode}, correlation {CorrelationId}, request {RequestId}, route {Route}",
			failure.Status, HttpProblemResponses.SafeCode(failure.Code, "operation.failed"), correlation, context.TraceIdentifier, route);
		await response.ExecuteAsync(context);
		return true;
	}
}
