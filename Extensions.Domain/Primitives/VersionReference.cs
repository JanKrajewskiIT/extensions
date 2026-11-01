namespace Extensions.Domain.Primitives;

/// <summary>Pins a named immutable version to its canonical content hash.</summary>
public sealed record VersionReference
{
    /// <summary>Requires nonempty identity/version and an exact content hash.</summary>
    public VersionReference(string id, string version, ContentHash contentHash)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        Id = DomainGuard.Required(id, nameof(id));
        Version = DomainGuard.Required(version, nameof(version));
        ContentHash = contentHash;
    }

    /// <summary>Publisher-owned document identity.</summary>
    public string Id { get; }
    /// <summary>Exact immutable version, not a latest-version selector.</summary>
    public string Version { get; }
    /// <summary>Integrity identity of the referenced content.</summary>
    public ContentHash ContentHash { get; }
}
