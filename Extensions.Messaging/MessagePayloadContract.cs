using System.Text.Json;

namespace Extensions.Messaging;

public record MessagePayloadContract<THash>(string SchemaId, int SchemaVersion, THash SchemaHash, JsonElement Definition);
