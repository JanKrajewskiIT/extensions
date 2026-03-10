using Extensions.Domain.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Extensions.Infrastructure.Security;

/// <summary>
/// HTTP-context-backed implementation of <see cref="ICurrentUserService"/>.
/// Reads identity and claims from the current request's <see cref="ClaimsPrincipal"/>.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
	public Guid? UserId =>
		Guid.TryParse(
			httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
			out var userId)
			? userId
			: null;

	public string? UserName => httpContextAccessor.HttpContext?.User?.Identity?.Name;

	public IReadOnlyList<string> Roles =>
		httpContextAccessor.HttpContext?.User?
			.FindAll(ClaimTypes.Role)
			.Select(c => c.Value)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList() ?? [];

	public bool IsInRole(string role) =>
		httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;

	public IReadOnlyDictionary<string, string> Claims =>
		httpContextAccessor.HttpContext?.User?.Claims
			.GroupBy(c => c.Type)
			.ToDictionary(g => g.Key, g => string.Join(", ", g.Select(c => c.Value)))
		?? new Dictionary<string, string>();
}
