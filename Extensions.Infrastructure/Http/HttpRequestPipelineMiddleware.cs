using Extensions.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Extensions.Infrastructure.Http;

internal sealed class HttpRequestPipelineMiddleware(RequestDelegate next, IOptions<HttpPipelineOptions> options,
	TimeProvider clock)
{
	public async Task InvokeAsync(HttpContext context, IRequestBudgetScopeFactory scopes)
	{
		HttpCorrelation.GetOrCreate(context);
		var settings = options.Value;
		var caller = context.RequestAborted;
		var duration = context.GetEndpoint()?.Metadata.GetMetadata<RequestBudgetLimit>()?.Duration ?? settings.DefaultRequestDuration;
		using var timeout = new CancellationTokenSource(duration, clock);
		using var request = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token);
		context.RequestAborted = request.Token;
		try
		{
			using var scope = scopes.Begin(context, request.Token);
			await next(context);
		}
		catch (OperationCanceledException failure) when (!caller.IsCancellationRequested && timeout.IsCancellationRequested)
		{
			throw new BusinessRuleException(settings.DeadlineCode, failure);
		}
		finally
		{
			context.RequestAborted = caller;
		}
	}
}
