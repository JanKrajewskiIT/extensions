using System.ComponentModel.DataAnnotations;

namespace Extensions.Infrastructure.Security;

public record KeycloakOptions
{
	[Required]
	public string Authority { get; init; } = string.Empty;

	[Required]
	public string Realm { get; init; } = string.Empty;

	public bool RequireHttpsMetadata { get; init; } = true;

	public string AuthorityUrl => $"{Authority}/realms/{Realm}";

	public Uri AuthorizationUrl => new($"{AuthorityUrl}/protocol/openid-connect/auth");

	public Uri TokenUrl => new($"{AuthorityUrl}/protocol/openid-connect/token");
}
