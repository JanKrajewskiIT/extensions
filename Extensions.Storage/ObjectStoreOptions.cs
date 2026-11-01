namespace Extensions.Storage;

/// <summary>Restart-bound storage settings. Credentials are supplied by the SDK chain or an explicit host factory.</summary>
public sealed class ObjectStoreOptions
{
	/// <summary>Default maximum immutable payload size.</summary>
	public const long DefaultMaximumObjectBytes = 1_073_741_824;
	/// <summary>Default SDK timeout; a host budget may impose a shorter deadline.</summary>
	public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(60);
	/// <summary>Bucket owned by the consuming service.</summary>
	public string Bucket { get; set; } = "";
	/// <summary>Manifest store identity.</summary>
	public string StoreName { get; set; } = "";
	/// <summary>SDK region and signing region for custom endpoints.</summary>
	public string Region { get; set; } = "";
	/// <summary>Optional trusted HTTPS origin; never a destination supplied by an API caller.</summary>
	public string? ServiceUrl { get; set; }
	/// <summary>Whether the endpoint requires path-style bucket addressing.</summary>
	public bool ForcePathStyle { get; set; }
	/// <summary>Opt-in to the explicit host credential factory; false uses the SDK credential chain.</summary>
	public bool UseStaticCredentials { get; set; }
	/// <summary>Maximum write and declared read size in bytes.</summary>
	public long MaximumObjectBytes { get; set; } = DefaultMaximumObjectBytes;
	/// <summary>SDK request timeout; no SDK retries are enabled by registration.</summary>
	public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;

	/// <summary>Validates non-secret settings without including their values in diagnostics.</summary>
	public static bool Valid(ObjectStoreOptions value) =>
		!string.IsNullOrWhiteSpace(value.Bucket) && value.Bucket.Length <= 63 &&
		!string.IsNullOrWhiteSpace(value.StoreName) && value.StoreName.Length <= 200 &&
		!string.IsNullOrWhiteSpace(value.Region) && value.Region.Length <= 200 && value.MaximumObjectBytes > 0 &&
		value.RequestTimeout > TimeSpan.Zero && value.RequestTimeout <= TimeSpan.FromMinutes(15) &&
		(value.ServiceUrl is null || ValidOrigin(value.ServiceUrl));

	private static bool ValidOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
		uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath == "/" && uri.UserInfo.Length == 0 &&
		uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
