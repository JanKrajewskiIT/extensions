using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public sealed class EfJobDependencies<TContext, TJob>(TContext context, TimeProvider clock)
	where TContext : DbContext where TJob : class
{
	public EfJobDependencies(TContext context, IJobCodec<TJob> codec, TimeProvider clock) : this(context, clock)
	{
		ArgumentNullException.ThrowIfNull(codec);
	}

	public async ValueTask<ImmutableArray<string>> MissingAsync(string owner, string scopeKey, ImmutableArray<RecurringJobDependency> dependencies,
		DateTimeOffset scheduledAt, CancellationToken token)
	{
		if (dependencies.IsDefaultOrEmpty)
		{
			return [];
		}

		var missing = ImmutableArray.CreateBuilder<string>();
		var now = clock.GetUtcNow();
		foreach (var dependency in dependencies)
		{
			var earliest = scheduledAt - dependency.MaximumAge;
			if (!await context.Set<JobRow>().AsNoTracking().AnyAsync(value => value.OwnerSubject == owner && value.Kind == dependency.Kind &&
				value.ScopeKey == scopeKey && value.Status == (int)JobStatus.Completed && value.CreatedAt >= earliest && value.CreatedAt <= now, token))
			{
				missing.Add(dependency.Kind);
			}
		}

		return missing.ToImmutable();
	}
}
