namespace Extensions.Domain.Abstractions;

/// <summary>
/// Static constants for role names used in authorization policies.
/// Values match Keycloak's default role naming convention.
/// </summary>
public static class Roles
{
    /// <summary>Administrator role with full access.</summary>
    public const string Admin = "admin";

    /// <summary>Moderator role with elevated permissions.</summary>
    public const string Moderator = "moderator";

    /// <summary>Standard authenticated user role.</summary>
    public const string User = "user";
}
