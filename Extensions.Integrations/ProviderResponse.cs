using System.Net;
using System.Text.Json;

namespace Extensions.Integrations;

public sealed record ProviderResponse(HttpStatusCode StatusCode, byte[] Content)
{
	public bool IsSuccess => (int)StatusCode is >= 200 and < 300;

	public JsonDocument Parse() => JsonDocument.Parse(Content);
}
