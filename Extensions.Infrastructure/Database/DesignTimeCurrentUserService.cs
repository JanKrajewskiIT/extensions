using Extensions.Domain.Abstractions;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Stub <see cref="ICurrentUserService"/> for EF Core design-time tools (migrations).
/// Returns safe defaults so HasQueryFilter expressions can compile without a real user context.
/// </summary>
public sealed class DesignTimeCurrentUserService : ICurrentUserService
{
	public Guid? UserId => Guid.Empty;
	public string? UserName => null;
	public IReadOnlyList<string> Roles => [];
	public IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
	public bool IsInRole(string role) => false;
}
