using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Extensions.Domain.Primitives;

namespace Extensions.Domain.Serialization;

/// <summary>Serializes canonical JSON with ordinal object keys and decimal numbers for stable persisted hashes.</summary>
public static class PayloadSerializer
{
    /// <summary>Read-only web JSON options preserving string enums, additive fields and the original depth limit.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Preserves array order while sorting object keys ordinally and normalizing decimal numbers.</summary>
    public static string Serialize<T>(T value) => Canonicalize(JsonSerializer.SerializeToElement(value, Options));

    /// <summary>Reads a non-null document using the same persisted wire representation.</summary>
    public static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new JsonException("The stored payload is null.");

    /// <summary>Hashes canonical UTF-8 JSON; insignificant property order does not change identity.</summary>
    public static ContentHash Hash(string value)
    {
        using var document = JsonDocument.Parse(value);
        return new ContentHash(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalize(document.RootElement)))));
    }

    private static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(element, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var value in element.EnumerateArray())
                {
                    Write(value, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (!element.TryGetDecimal(out var number))
                {
                    throw new JsonException("A numeric payload exceeds the supported decimal range.");
                }

                writer.WriteNumberValue(number);
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            AllowOutOfOrderMetadataProperties = true,
            MaxDepth = 64,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
