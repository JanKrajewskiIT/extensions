using Extensions.Domain.Primitives;

namespace Extensions.Application.Pagination;

/// <summary>Continuation bound to its filter and last immutable identity.</summary>
public sealed record JournalIdentityCursor(ContentHash Filter, Guid AfterId);
