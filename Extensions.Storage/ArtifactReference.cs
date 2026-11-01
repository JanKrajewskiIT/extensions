using Extensions.Domain.Primitives;

namespace Extensions.Storage;

/// <summary>Exact immutable object identity; it does not grant access to the referenced object.</summary>
public sealed record ArtifactReference
{
	/// <summary>Retains the store, key, exact version, media type, byte length and SHA-256 manifest.</summary>
	public ArtifactReference(string store, string objectKey, string objectVersion, string mediaType, long length, ContentHash contentHash)
	{
		ArgumentNullException.ThrowIfNull(contentHash);
		ArgumentOutOfRangeException.ThrowIfNegative(length);
		Store = DomainGuard.Required(store, nameof(store));
		ObjectKey = DomainGuard.Required(objectKey, nameof(objectKey));
		ObjectVersion = DomainGuard.Required(objectVersion, nameof(objectVersion));
		MediaType = DomainGuard.Required(mediaType, nameof(mediaType));
		Length = length;
		ContentHash = contentHash;
	}

	/// <summary>Host-defined store identity.</summary>
	public string Store { get; }
	/// <summary>Complete content-addressed key.</summary>
	public string ObjectKey { get; }
	/// <summary>Exact object version, never a lookup of the latest version.</summary>
	public string ObjectVersion { get; }
	/// <summary>Declared content type.</summary>
	public string MediaType { get; }
	/// <summary>Exact payload length in bytes.</summary>
	public long Length { get; }
	/// <summary>SHA-256 of the payload bytes.</summary>
	public ContentHash ContentHash { get; }
}
