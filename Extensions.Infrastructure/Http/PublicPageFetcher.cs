using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Infrastructure.Http;

/// <summary>
/// Fetches user-supplied URLs without letting them reach internal resources.
/// Every redirect hop is validated with <see cref="UrlSafetyValidator"/>, and the connection itself only goes to public addresses,
/// which also closes the window between DNS validation and connect (DNS rebinding).
/// The client must be registered with <see cref="WithPublicInternetOnly"/>.
/// </summary>
public static class PublicPageFetcher
{
    private const int MaxRedirects = 3;

    /// <summary>Disables automatic redirects and restricts connections to public addresses.</summary>
    public static IHttpClientBuilder WithPublicInternetOnly(this IHttpClientBuilder builder)
        => builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = ConnectToPublicAddressAsync
        });

    /// <summary>Downloads the page body, following at most three redirects.</summary>
    public static async Task<string> GetPublicPageAsync(
        this HttpClient client,
        string url,
        CancellationToken cancellationToken = default)
    {
        var current = new Uri(url, UriKind.Absolute);

        for (var redirects = 0; ; redirects++)
        {
            await UrlSafetyValidator.ValidateAsync(current.AbsoluteUri, cancellationToken);

            using var response = await client.GetAsync(current, cancellationToken);
            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }

            if (redirects >= MaxRedirects)
                throw new HttpRequestException($"More than {MaxRedirects} redirects were followed.");

            current = new Uri(current, response.Headers.Location);
        }
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        IPAddress[] addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || !addresses.All(UrlSafetyValidator.IsPublicAddress))
            throw new HttpRequestException("Access to internal hosts is not allowed.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
}
