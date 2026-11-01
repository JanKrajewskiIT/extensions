using Extensions.Domain.Primitives;

namespace Extensions.Application.Pagination;

/// <summary>Continuation bound to its filter, knowledge cutoff and stable document order.</summary>
public sealed record JournalCursor(
    ContentHash Filter,
    DateTimeOffset Through,
    DateTimeOffset PublishedAt,
    string Kind,
    string Id,
    string Version);
