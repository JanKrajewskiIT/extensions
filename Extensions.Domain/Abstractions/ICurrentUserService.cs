namespace Extensions.Domain.Abstractions;

/// <summary>
/// Service to get current authenticated user information.
/// </summary>
public interface ICurrentUserService
{
	Guid? UserId { get; }
	string? UserName { get; }
	IReadOnlyList<string> Roles { get; }
	bool IsInRole(string role);
	IReadOnlyDictionary<string, string> Claims { get; }
}
