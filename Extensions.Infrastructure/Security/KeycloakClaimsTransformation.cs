using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace Extensions.Infrastructure.Security;

/// <summary>
/// Transforms Keycloak JWT claims to standard ASP.NET Core role claims.
/// This runs once per authentication, making role checks efficient.
/// </summary>
public sealed class KeycloakClaimsTransformation(ILogger<KeycloakClaimsTransformation> logger) : IClaimsTransformation
{
    private const string RealmAccessClaim = "realm_access";
    private const string ResourceAccessClaim = "resource_access";
    private const string TransformedMarker = "keycloak_roles_transformed";

    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        if (identity.HasClaim(c => c.Type == TransformedMarker))
        {
            return Task.FromResult(principal);
        }

        var roles = new List<string>();

        var realmAccess = principal.FindFirst(RealmAccessClaim)?.Value;
        if (realmAccess is not null)
        {
            roles.AddRange(ExtractRolesFromJson(realmAccess));
        }

        var resourceAccess = principal.FindFirst(ResourceAccessClaim)?.Value;
        if (resourceAccess is not null)
        {
            roles.AddRange(ExtractClientRolesFromJson(resourceAccess));
        }

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        identity.AddClaim(new Claim(TransformedMarker, "true"));

        return Task.FromResult(principal);
    }

    private IEnumerable<string> ExtractRolesFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == JsonValueKind.Array)
            {
                return rolesElement.EnumerateArray()
                    .Where(r => r.ValueKind == JsonValueKind.String)
                    .Select(r => r.GetString()!)
                    .ToList();
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse realm_access roles from JWT claim");
        }

        return [];
    }

    private IEnumerable<string> ExtractClientRolesFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var roles = new List<string>();

            foreach (var client in doc.RootElement.EnumerateObject())
            {
                if (client.Value.TryGetProperty("roles", out var rolesElement) &&
                    rolesElement.ValueKind == JsonValueKind.Array)
                {
                    roles.AddRange(rolesElement.EnumerateArray()
                        .Where(r => r.ValueKind == JsonValueKind.String)
                        .Select(r => r.GetString()!));
                }
            }

            return roles;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse resource_access roles from JWT claim");
        }

        return [];
    }
}
