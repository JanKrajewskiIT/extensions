using System.ComponentModel.DataAnnotations;

namespace Extensions.Infrastructure.Security;

public record KeycloakOptions(
	[Required] string Authority,
	[Required] string Realm,
	bool RequireHttpsMetadata = true)
{
	public KeycloakOptions() : this(null!, null!) { }

	public string AuthorityUrl => $"{Authority}/realms/{Realm}";

	public Uri AuthorizationUrl => new($"{AuthorityUrl}/protocol/openid-connect/auth");

	public Uri TokenUrl => new($"{AuthorityUrl}/protocol/openid-connect/token");
}