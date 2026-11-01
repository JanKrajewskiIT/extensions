namespace Extensions.Storage;

/// <summary>Exact-version deletion, called only after the host authorizes retirement.</summary>
public interface IArtifactVersionRemover
{
	/// <summary>Deletes only the version identified by the complete manifest.</summary>
	ValueTask RemoveVersionAsync(ArtifactReference artifact, CancellationToken cancellationToken = default);
}
