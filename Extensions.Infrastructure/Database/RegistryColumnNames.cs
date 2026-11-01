namespace Extensions.Infrastructure.Database;

/// <summary>Host column mapping; legacy resource names need not become the library's domain vocabulary.</summary>
public sealed record RegistryColumnNames(string Owner = nameof(RegistryDocumentStamp.Owner),
	string Kind = nameof(RegistryDocumentStamp.Kind), string Id = nameof(RegistryDocumentStamp.Id),
	string Version = nameof(RegistryDocumentStamp.Version), string ContentHash = nameof(RegistryDocumentStamp.ContentHash),
	string Payload = nameof(RegistryDocumentStamp.Payload), string ResourceId = nameof(RegistryDocumentStamp.ResourceId),
	string ScopeKey = nameof(RegistryDocumentStamp.ScopeKey), string AvailableAt = nameof(RegistryDocumentStamp.AvailableAt),
	string PublishedAt = nameof(RegistryDocumentStamp.PublishedAt));
