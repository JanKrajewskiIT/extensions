namespace Extensions.Domain.Abstractions;

/// <summary>
/// Service to get current authenticated user information.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Gets the current user's unique identifier, or <c>null</c> if unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Gets the current user's display name, or <c>null</c> if unavailable.</summary>
    string? UserName { get; }

    /// <summary>Gets all roles assigned to the current user.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Returns <c>true</c> if the current user has the specified <paramref name="role"/>.</summary>
    bool IsInRole(string role);

    /// <summary>Gets all claims for the current user, keyed by claim type (multiple values joined with ", ").</summary>
    IReadOnlyDictionary<string, string> Claims { get; }
}
