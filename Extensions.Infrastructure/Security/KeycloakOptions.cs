using System.ComponentModel.DataAnnotations;

namespace Extensions.Infrastructure.Security;

/// <summary>
/// Keycloak connection settings. Bind to the <c>"Keycloak"</c> configuration section.
/// </summary>
public record KeycloakOptions
{
    /// <summary>Gets the base URL of the Keycloak server (e.g. <c>https://auth.example.com</c>).</summary>
    [Required]
    public string Authority { get; init; } = string.Empty;

    /// <summary>Gets the Keycloak realm name.</summary>
    [Required]
    public string Realm { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether HTTPS metadata is required. Defaults to <c>true</c>; disable only in Development.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>Gets the full issuer URL for the realm (<c>{Authority}/realms/{Realm}</c>).</summary>
    public string AuthorityUrl => $"{Authority}/realms/{Realm}";

    /// <summary>Gets the OAuth2 authorization endpoint URL.</summary>
    public Uri AuthorizationUrl => new($"{AuthorityUrl}/protocol/openid-connect/auth");

    /// <summary>Gets the OAuth2 token endpoint URL.</summary>
    public Uri TokenUrl => new($"{AuthorityUrl}/protocol/openid-connect/token");
}
