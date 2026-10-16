using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Ai;

/// <summary>Opt-in HTTP mapping for AI execution failures; unrelated dependency failures pass through.</summary>
public sealed class AiFailureFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AiRequestTimeoutException)
        {
            return Results.Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "AI operation timed out",
                extensions: new Dictionary<string, object?> { ["code"] = "ai_timeout" });
        }
        catch (AiOutputLimitException)
        {
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "AI response exceeded its limit",
                extensions: new Dictionary<string, object?> { ["code"] = "ai_output_limit" });
        }
    }
}
