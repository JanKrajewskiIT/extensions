using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Explicit owner participants; admissions precede fences and observations follow them.</summary>
public sealed record DatabaseSavePlan(
	bool RequiresAsynchronousSave,
	Func<DbContext, CancellationToken, ValueTask>? Admit = null,
	Func<DbContext, CancellationToken, ValueTask>? Observe = null,
	Func<ICriticalWriteAudit?>? Audit = null)
{
	/// <summary>No host participants; ambient fences still apply.</summary>
	public static DatabaseSavePlan Empty { get; } = new(false);
}
