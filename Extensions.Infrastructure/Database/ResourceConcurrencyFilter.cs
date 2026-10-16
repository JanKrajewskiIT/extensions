using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Opt-in mapping for EF concurrency conflicts; revision validation remains host-owned.</summary>
public sealed class ResourceConcurrencyFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Resource changed",
                extensions: new Dictionary<string, object?> { ["code"] = "resource_conflict" });
        }
    }
}
