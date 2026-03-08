using System.Net;
using System.Net.Sockets;

namespace Extensions.Infrastructure.Http;

/// <summary>
/// Validates URLs to prevent SSRF (Server-Side Request Forgery) attacks.
/// Blocks requests to internal hosts, private networks, and non-HTTP(S) schemes.
/// </summary>
public static class UrlSafetyValidator
{
	private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
	{
		"localhost", "127.0.0.1", "0.0.0.0", "::1",
		"metadata.google.internal", "169.254.169.254"
	};

	/// <summary>
	/// Validates that a URL is safe to fetch (not targeting internal resources).
	/// Throws <see cref="ArgumentException"/> if the URL is invalid or targets an internal host.
	/// </summary>
	public static void Validate(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
			throw new ArgumentException("Invalid URL format.", nameof(url));

		if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
			throw new ArgumentException("Only HTTP and HTTPS URLs are allowed.", nameof(url));

		if (BlockedHosts.Contains(uri.Host))
			throw new ArgumentException("Access to internal hosts is not allowed.", nameof(url));

		if (IPAddress.TryParse(uri.Host, out var parsedIp))
		{
			EnsurePublicAddress(parsedIp, url);
			return;
		}

		IPAddress[] resolvedAddresses;
		try
		{
			resolvedAddresses = Dns.GetHostAddresses(uri.DnsSafeHost);
		}
		catch (SocketException)
		{
			throw new ArgumentException("Host cannot be resolved.", nameof(url));
		}

		if (resolvedAddresses.Length == 0)
			throw new ArgumentException("Host cannot be resolved.", nameof(url));

		foreach (var resolvedAddress in resolvedAddresses)
		{
			EnsurePublicAddress(resolvedAddress, url);
		}
	}

	private static void EnsurePublicAddress(IPAddress ip, string url)
	{
		if (IPAddress.IsLoopback(ip))
			throw new ArgumentException("Access to loopback addresses is not allowed.", nameof(url));

		if (ip.AddressFamily == AddressFamily.InterNetworkV6)
		{
			if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IsUniqueLocalIpv6(ip))
				throw new ArgumentException("Access to private network addresses is not allowed.", nameof(url));

			return;
		}

		if (IsPrivateOrReservedIpv4(ip))
			throw new ArgumentException("Access to private network addresses is not allowed.", nameof(url));
	}

	private static bool IsPrivateOrReservedIpv4(IPAddress ip)
	{
		var bytes = ip.GetAddressBytes();
		return bytes switch
		{
			[10, ..] => true,
			[172, >= 16 and <= 31, ..] => true,
			[192, 168, ..] => true,
			[169, 254, ..] => true,
			[127, ..] => true,
			_ => false
		};
	}

	private static bool IsUniqueLocalIpv6(IPAddress ip)
	{
		var bytes = ip.GetAddressBytes();
		return bytes.Length > 0 && (bytes[0] & 0b1111_1110) == 0b1111_1100;
	}
}