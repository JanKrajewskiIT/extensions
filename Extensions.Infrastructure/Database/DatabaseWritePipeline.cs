using Microsoft.EntityFrameworkCore;

namespace Extensions.Infrastructure.Database;

internal sealed class DatabaseWritePipeline
{
	internal static void RequireSynchronousSave(DbContext context, DatabaseSavePlan plan)
	{
		if (OperationWriteScope.For(context).Length > 0 || plan.RequiresAsynchronousSave || plan.Observe is not null || plan.Audit is not null)
		{
			throw new InvalidOperationException("Fenced operations require asynchronous transactional writes.");
		}
	}

	internal async Task<int> SaveAsync(DbContext context, DatabaseSavePlan plan,
		Func<bool, CancellationToken, Task<int>> persist, bool acceptAllChangesOnSuccess, CancellationToken token)
	{
		var fences = OperationWriteScope.For(context);
		if (fences.Length == 0 && plan.Admit is null && plan.Observe is null && plan.Audit is null)
		{
			return await persist(acceptAllChangesOnSuccess, token);
		}

		token.ThrowIfCancellationRequested();
		DatabaseTransactions.RequireCompatibleExecutionStrategy(context);
		await using var transaction = context.Database.CurrentTransaction is null
			? await context.Database.BeginTransactionAsync(token) : null;
		if (plan.Admit is not null)
		{
			await plan.Admit(context, token);
		}

		foreach (var fence in fences)
		{
			await fence.RequireAsync(context, token);
		}

		if (plan.Observe is not null)
		{
			await plan.Observe(context, token);
		}

		IReadOnlyList<object> auditRows = [];
		int saved;
		try
		{
			if (plan.Audit?.Invoke() is { } audit)
			{
				auditRows = await audit.StageAsync(context, token);
			}

			saved = await persist(false, token);
			foreach (var fence in fences)
			{
				await fence.RequireAsync(context, token);
			}

			if (transaction is not null)
			{
				await transaction.CommitAsync(token);
			}
		}
		catch
		{
			foreach (var row in auditRows)
			{
				context.Entry(row).State = EntityState.Detached;
			}

			throw;
		}

		if (acceptAllChangesOnSuccess)
		{
			context.ChangeTracker.AcceptAllChanges();
		}

		return saved;
	}
}
