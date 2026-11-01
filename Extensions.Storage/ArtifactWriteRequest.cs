using Extensions.Domain.Primitives;

namespace Extensions.Storage;

/// <summary>Declared immutable write; hosts supply and authorize opaque retention and entitlement versions.</summary>
public sealed record ArtifactWriteRequest(string ObjectKey, string MediaType, long ContentLength,
	ContentHash ExpectedHash, VersionReference RetentionPolicy, VersionReference Entitlement);
