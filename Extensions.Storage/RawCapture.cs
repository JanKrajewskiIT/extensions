using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Extensions.Domain.Primitives;

namespace Extensions.Storage;

/// <summary>Byte-preserving raw JSON capture; the host supplies the source key and policy versions.</summary>
public static class RawCapture
{
	/// <summary>Retains raw JSON bytes and the established raw/provider/hash key convention, then registers dependencies.</summary>
	public static async ValueTask<ArtifactReference> WriteAsync(IVersionedArtifactStore store, string provider,
		JsonElement record, VersionReference retention, VersionReference entitlement, IArtifactDependencyRecorder dependencies, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(dependencies);
		DomainGuard.Required(provider, nameof(provider));
		var bytes = Encoding.UTF8.GetBytes(record.GetRawText());
		var hash = new ContentHash(Convert.ToHexString(SHA256.HashData(bytes)));
		await using var content = new MemoryStream(bytes, writable: false);
		var artifact = await store.WriteImmutableAsync(new ArtifactWriteRequest($"raw/{provider}/{hash.Sha256}.json",
			"application/json", bytes.Length, hash, retention, entitlement), content, cancellationToken);
		await dependencies.RecordAsync(artifact, Encoding.UTF8.GetString(bytes), cancellationToken);
		return artifact;
	}
}
