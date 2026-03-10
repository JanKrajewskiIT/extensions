using Extensions.Domain.Abstractions;

namespace Extensions.Infrastructure.Database;

/// <summary>
/// Stub <see cref="ICurrentUserService"/> for EF Core design-time tools (migrations).
/// Returns safe defaults so HasQueryFilter expressions can compile without a real user context.
/// </summary>
public sealed class DesignTimeCurrentUserService : ICurrentUserService
{
	/// <inheritdoc />
	public Guid? UserId => Guid.Empty;

	/// <inheritdoc />
	public string? UserName => null;

	/// <inheritdoc />
	public IReadOnlyList<string> Roles => [];

	/// <inheritdoc />
	public IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();

	/// <inheritdoc />
	public bool IsInRole(string role) => false;
}
