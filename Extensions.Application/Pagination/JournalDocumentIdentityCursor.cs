using Extensions.Domain.Primitives;

namespace Extensions.Application.Pagination;

/// <summary>Continuation bound to its filter and last document version.</summary>
public sealed record JournalDocumentIdentityCursor(ContentHash Filter, string Id, string Version);
