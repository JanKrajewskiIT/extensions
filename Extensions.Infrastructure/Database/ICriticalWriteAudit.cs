using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Stages local commit evidence; central delivery is outside this transaction boundary.</summary>
public interface ICriticalWriteAudit
{
	/// <summary>Returns the newly tracked audit rows so a failed save can detach exactly those rows.</summary>
	ValueTask<IReadOnlyList<object>> StageAsync(DbContext context, CancellationToken token);
}
