namespace Extensions.Domain.Abstractions;

/// <summary>
/// Static constants for role names used in authorization policies.
/// Values match Keycloak's default role naming convention.
/// </summary>
public static class Roles
{
	public const string Admin = "admin";
	public const string Moderator = "moderator";
	public const string User = "user";
}
