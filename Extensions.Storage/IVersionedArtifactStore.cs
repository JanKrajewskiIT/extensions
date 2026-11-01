namespace Extensions.Storage;

/// <summary>Immutable versioned storage; the host owns authorization and policy enforcement.</summary>
public interface IVersionedArtifactStore
{
	/// <summary>Validates declared bytes and returns the exact stored version.</summary>
	ValueTask<ArtifactReference> WriteImmutableAsync(ArtifactWriteRequest request, Stream content, CancellationToken cancellationToken = default);
	/// <summary>Opens an exact version; consume to EOF to complete streaming checksum verification.</summary>
	ValueTask<Stream> OpenReadAsync(ArtifactReference artifact, CancellationToken cancellationToken = default);
}
