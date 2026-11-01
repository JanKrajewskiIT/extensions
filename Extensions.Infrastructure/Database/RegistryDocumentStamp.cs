namespace Extensions.Infrastructure.Database;

/// <summary>Neutral immutable document metadata; identifiers, scope and optional resource belong to the host.</summary>
public sealed record RegistryDocumentStamp(string Owner, string Kind, string Id, string Version, string ContentHash,
	string Payload, string? ScopeKey, Guid? ResourceId, DateTimeOffset AvailableAt, DateTimeOffset PublishedAt,
	DateTimeOffset? RevokedAt = null);
