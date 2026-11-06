using System.Text.Json;

namespace Extensions.Infrastructure.Http;

public sealed record JsonPayloadSchema(string Sha256, JsonElement Definition);
