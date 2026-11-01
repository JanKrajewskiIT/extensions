using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

/// <summary>Stages an owner's projected rows only after the complete projection has succeeded.</summary>
public sealed class CriticalAuditStager(Func<DbContext, CancellationToken, ValueTask<IReadOnlyList<object>>> project) : ICriticalWriteAudit
{
	private readonly Func<DbContext, CancellationToken, ValueTask<IReadOnlyList<object>>> projection =
		project ?? throw new ArgumentNullException(nameof(project));

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<object>> StageAsync(DbContext context, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(context);
		var rows = await projection(context, token);
		try
		{
			context.AddRange(rows);
			return rows;
		}
		catch
		{
			foreach (var row in rows)
			{
				context.Entry(row).State = EntityState.Detached;
			}

			throw;
		}
	}
}
