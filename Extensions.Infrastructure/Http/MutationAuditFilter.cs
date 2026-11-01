using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Opt-in mutation observation with host-owned authority and evidence policies.</summary>
public sealed class MutationAuditFilter(IMutationAuditPolicy policy) : IEndpointFilter
{
	/// <inheritdoc />
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		var http = context.HttpContext;
		if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) ||
			http.GetEndpoint()?.Metadata.GetMetadata<ReadOnlyOperation>() is not null)
		{
			return await next(context);
		}

		if (policy.RequiresStrongAuthentication(http) && !policy.IsStronglyAuthenticated(http))
		{
			return Results.Forbid();
		}

		var operation = await policy.PrepareAsync(context, http.RequestAborted);
		return await operation.ExecuteAsync(async token =>
		{
			for (var index = 0; index < context.Arguments.Count; index++)
			{
				if (context.Arguments[index] is CancellationToken)
				{
					context.Arguments[index] = token;
				}
			}

			return await next(context);
		}, http.RequestAborted);
	}
}
