using System.Collections.Immutable;

namespace Extensions.Application.Pagination;

/// <summary>A cursor page without offset or total-count assumptions.</summary>
public sealed record JournalPage<T>(ImmutableArray<T> Items, string? NextCursor);
