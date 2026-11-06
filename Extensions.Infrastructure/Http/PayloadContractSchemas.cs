using System.Text.Json;
using Extensions.Domain.Serialization;

namespace Extensions.Infrastructure.Http;

public static class PayloadContractSchemas
{
	private const int MaximumCachedSchemas = 512;
	private static readonly object gate = new();
	private static readonly Dictionary<(Type Type, JsonSerializerOptions Options), JsonPayloadSchema> schemas = [];

	public static JsonPayloadSchema Describe<T>(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (!options.IsReadOnly)
		{
			throw new ArgumentException("Schema caching requires read-only serialization options.", nameof(options));
		}

		lock (gate)
		{
			var key = (typeof(T), options);
			if (schemas.TryGetValue(key, out var schema))
			{
				return schema;
			}

			var definition = JsonSerializer.SerializeToElement(ContractSchemas.Export(typeof(T), options));
			schema = new JsonPayloadSchema(PayloadSerializer.Hash(definition.GetRawText()).Sha256, definition);
			if (schemas.Count < MaximumCachedSchemas)
			{
				schemas.Add(key, schema);
			}

			return schema;
		}
	}
}
