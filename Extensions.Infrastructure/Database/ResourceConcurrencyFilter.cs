using Extensions.Infrastructure.Http;
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
            return HttpProblemResponses.Problem(context.HttpContext,
                new HttpFailure(StatusCodes.Status409Conflict, "resource_conflict"));
        }
    }
}
