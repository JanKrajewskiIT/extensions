using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>
/// Forwards the incoming request's Bearer token to outgoing HTTP requests
/// for service-to-service authenticated calls via Aspire service discovery.
/// </summary>
public sealed class AuthTokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(token) &&
            token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token["Bearer ".Length..]);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
