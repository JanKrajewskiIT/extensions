namespace Extensions.Storage;

/// <summary>Explicit owner hook for registering payload dependencies after an immutable write.</summary>
public interface IArtifactDependencyRecorder
{
	/// <summary>Records the exact object and JSON payload under the host's authority and recovery protocol.</summary>
	ValueTask RecordAsync(ArtifactReference artifact, string payload, CancellationToken cancellationToken = default);
}
