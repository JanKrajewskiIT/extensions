using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Extensions.Infrastructure.Http;

/// <summary>Neutral JSON Schema exporter; the host supplies types, serialization and transforms.</summary>
public static class ContractSchemas
{
	/// <summary>Exports schema-only metadata without changing runtime constructor serialization.</summary>
	public static JsonNode Export(Type type, JsonSerializerOptions options,
		JsonSchemaExporterOptions? exporterOptions = null)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(options);
		exporterOptions ??= JsonSchemaExporterOptions.Default;
		var resolver = new SchemaResolver(options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(), options);
		var schemaOptions = new JsonSerializerOptions(options) { TypeInfoResolver = resolver };
		schemaOptions.MakeReadOnly();
		return schemaOptions.GetJsonSchemaAsNode(type, new JsonSchemaExporterOptions
		{
			TreatNullObliviousAsNonNullable = exporterOptions.TreatNullObliviousAsNonNullable,
			TransformSchemaNode = (context, node) =>
			{
				if (context.PropertyInfo is { } property &&
					resolver.Defaults.TryGetValue(property, out var defaultValue))
				{
					if (node is not JsonObject)
					{
						node = new JsonObject { ["allOf"] = new JsonArray(node) };
					}

					node["default"] = defaultValue?.DeepClone();
				}

				return exporterOptions.TransformSchemaNode?.Invoke(context, node) ?? node;
			},
		});
	}

	private sealed class SchemaResolver(IJsonTypeInfoResolver resolver, JsonSerializerOptions sourceOptions)
		: IJsonTypeInfoResolver
	{
		public Dictionary<JsonPropertyInfo, JsonNode?> Defaults { get; } = [];

		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
		{
			var source = resolver.GetTypeInfo(type, options);
			if (source is not { Kind: JsonTypeInfoKind.Object } || !source.Properties.Any(HasUnrepresentableDefault))
			{
				return source;
			}

			// .NET 11 RC2 serializes null reflection defaults as non-nullable structs before schema transforms.
			// Build schema-only metadata without constructor associations; keep runtime serialization unchanged.
			var schema = JsonTypeInfo.CreateJsonTypeInfo(type, options);
			schema.NumberHandling = source.NumberHandling;
			schema.UnmappedMemberHandling = source.UnmappedMemberHandling;
			schema.PreferredPropertyObjectCreationHandling = source.PreferredPropertyObjectCreationHandling;
			if (source.PolymorphismOptions is { } polymorphism)
			{
				schema.PolymorphismOptions = new JsonPolymorphismOptions
				{
					TypeDiscriminatorPropertyName = polymorphism.TypeDiscriminatorPropertyName,
					IgnoreUnrecognizedTypeDiscriminators = polymorphism.IgnoreUnrecognizedTypeDiscriminators,
					UnknownDerivedTypeHandling = polymorphism.UnknownDerivedTypeHandling,
				};
				foreach (var derivedType in polymorphism.DerivedTypes)
				{
					schema.PolymorphismOptions.DerivedTypes.Add(derivedType);
				}
			}

			foreach (var property in source.Properties)
			{
				var copy = schema.CreateJsonPropertyInfo(property.PropertyType, property.Name);
				copy.Get = property.Get;
				copy.Set = property.Set;
				copy.ShouldSerialize = property.ShouldSerialize;
				copy.CustomConverter = property.CustomConverter;
				copy.NumberHandling = property.NumberHandling;
				copy.ObjectCreationHandling = property.ObjectCreationHandling;
				copy.IsExtensionData = property.IsExtensionData;
				copy.IsGetNullable = property.IsGetNullable;
				copy.IsSetNullable = property.IsSetNullable;
				copy.IsRequired = property.IsRequired ||
					property.AssociatedParameter is { HasDefaultValue: false, IsMemberInitializer: false };
				copy.Order = property.Order;
				copy.AttributeProvider = property.AttributeProvider;
				schema.Properties.Add(copy);
				if (property.AssociatedParameter is { HasDefaultValue: true } parameter &&
					!HasUnrepresentableDefault(property))
				{
					Defaults.Add(copy,
						JsonSerializer.SerializeToNode(parameter.DefaultValue, property.PropertyType, sourceOptions));
				}
			}

			return schema;
		}

		private static bool HasUnrepresentableDefault(JsonPropertyInfo property) =>
			property.AssociatedParameter is { HasDefaultValue: true, DefaultValue: null } &&
			property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null;
	}
}
