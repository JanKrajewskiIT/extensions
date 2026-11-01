using System.Text.Json;

namespace Extensions.Infrastructure.Database;

/// <summary>Owner-supplied schema admission; the shared library does not discover document catalogs.</summary>
public interface IRegistryDocumentAdmission<TReference> where TReference : class
{
	/// <summary>Validates a document and optionally returns its exact schema reference.</summary>
	TReference? Validate(string kind, TReference reference, string payload);

	/// <summary>Describes a schema owned by the publisher.</summary>
	JsonElement Describe(TReference schema);
}
