using System.Text.Json;
using Extensions.Domain.Primitives;

namespace Extensions.Application.Documents;

/// <summary>A version-pinned document projection retaining publication and revocation times.</summary>
public sealed record JournalDocument(
    string Kind,
    VersionReference Reference,
    DateTimeOffset PublishedAt,
    DateTimeOffset? RevokedAt,
    JsonElement Content);
