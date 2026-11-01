using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Extensions.Domain.Primitives;
using Extensions.Domain.Serialization;

namespace Extensions.Storage;

/// <summary>Canonical JSON artifacts with an explicit owner dependency-registration hook.</summary>
public static class JsonArtifacts
{
	/// <summary>Writes canonical bytes and awaits owner dependency registration before returning success.</summary>
	public static async ValueTask<ArtifactReference> WriteAsync<T>(IVersionedArtifactStore store,
		string key, T value, VersionReference retention, VersionReference entitlement, IArtifactDependencyRecorder dependencies,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(dependencies);
		var bytes = Encoding.UTF8.GetBytes(PayloadSerializer.Serialize(value));
		await using var content = new MemoryStream(bytes, writable: false);
		var artifact = await store.WriteImmutableAsync(new ArtifactWriteRequest(key, "application/json", bytes.Length,
			new ContentHash(Convert.ToHexString(SHA256.HashData(bytes))), retention, entitlement), content, cancellationToken);
		await dependencies.RecordAsync(artifact, Encoding.UTF8.GetString(bytes), cancellationToken);

		return artifact;
	}

	/// <summary>Deserializes an exact version and consumes EOF so the store completes integrity verification.</summary>
	public static async ValueTask<T> ReadAsync<T>(IVersionedArtifactStore store, ArtifactReference artifact,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(artifact);
		await using var content = await store.OpenReadAsync(artifact, cancellationToken);
		var value = await JsonSerializer.DeserializeAsync<T>(content, PayloadSerializer.Options, cancellationToken)
			?? throw new JsonException("The artifact payload is null.");
		var remainder = new byte[1];
		if (await content.ReadAsync(remainder, cancellationToken) != 0)
		{
			throw new InvalidDataException("An artifact contains trailing data.");
		}

		return value;
	}
}
