using Microsoft.AspNetCore.Http;

namespace Extensions.Infrastructure.Http;

/// <summary>Host hook for dependency budgets and write fences; it must not detach request work.</summary>
public interface IRequestBudgetScopeFactory
{
	/// <summary>Enters a scope whose lifetime covers the entire awaited request.</summary>
	IDisposable Begin(HttpContext context, CancellationToken cancellationToken);
}
