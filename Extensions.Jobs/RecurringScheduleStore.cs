using System.Collections.Immutable;
using Extensions.Domain.Serialization;
using Extensions.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public class RecurringScheduleStore<TContext, TSchedule, TLease, TJob>(TContext context, TimeProvider clock,
	IRecurringScheduleCodec<TSchedule, TLease> codec, IJobCodec<TJob> jobs, IRecurringPersistenceEvents<TLease, TJob> events)
	where TContext : DbContext where TSchedule : class where TLease : RecurringOccurrenceLease<TSchedule>, IRecurringOccurrence where TJob : class
{
	public const string OccurrenceKind = "recurring-occurrence";

	public static readonly TimeSpan LeaseDuration = RecurringScheduleTiming.LeaseDuration;

	public static Guid CommandId(TLease lease)
	{
		return RecurringOccurrenceKeys.CommandId(lease);
	}

	public static Guid JobId(TLease lease)
	{
		return RecurringOccurrenceKeys.JobId(lease);
	}

	public static string IdempotencyKey(TLease lease)
	{
		return RecurringOccurrenceKeys.IdempotencyKey(lease.IdempotencyPrefix, lease);
	}

	public static ValueTask LockScheduleAsync(DbContext database, string owner, string id, CancellationToken token)
	{
		return database.LockAsync(DatabaseLockKeys.Create("recurring-schedule", owner, id), token);
	}

	public async ValueTask<TLease?> AcquireAsync(TSchedule schedule, string owner, CancellationToken token)
	{
		var definition = codec.Describe(schedule);
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockScheduleAsync(context, owner, definition.Id, token);
		if (!await IsCurrentDefinitionAsync(schedule, owner, token))
		{
			return null;
		}

		var now = clock.GetUtcNow();
		var row = await FindAsync(owner, definition.Id, token);
		if (row is null)
		{
			row = new RecurringScheduleStateRow
			{
				Owner = owner,
				ScheduleId = definition.Id,
				DefinitionVersion = definition.Version,
				NextRunAt = Timestamp(definition.FirstRunAt),
				UpdatedAt = now,
				Version = 1,
			};
			context.Set<RecurringScheduleStateRow>().Add(row);
		}
		else if (row.DefinitionVersion != definition.Version)
		{
			row.NextRunAt = Timestamp(RecurringScheduleTiming.NextRunAt(definition, row));
			row.DefinitionVersion = definition.Version;
			row.LeaseUntil = null;
			row.LeaseId = null;
			row.UpdatedAt = now;
			row.Version = checked(row.Version + 1);
		}

		if (row.LeaseUntil > now || row.NextRunAt > now)
		{
			await context.SaveChangesAsync(token);
			await transaction.CommitAsync(token);
			return null;
		}

		if (definition.MisfirePolicy == RecurringMisfirePolicy.RunOnce && now - row.NextRunAt >= definition.Interval)
		{
			row.NextRunAt = Timestamp(row.NextRunAt.AddTicks((now.UtcTicks - row.NextRunAt.UtcTicks) / definition.Interval.Ticks * definition.Interval.Ticks));
		}

		var deadline = now + definition.ExecutionWindow;
		var lease = codec.CreateLease(schedule, owner, row.NextRunAt, Guid.NewGuid(), deadline);
		row.LeaseId = lease.LeaseId;
		row.LeaseUntil = Minimum(now + LeaseDuration, deadline);
		row.UpdatedAt = now;
		row.Version = checked(row.Version + 1);
		await context.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
		return lease;
	}

	public async ValueTask RenewAsync(TLease lease, CancellationToken token)
	{
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockScheduleAsync(context, lease.Owner, lease.ScheduleId, token);
		var row = await RequireLeaseAsync(lease, token);
		var now = clock.GetUtcNow();
		row.LeaseUntil = Minimum(now + LeaseDuration, lease.PreparationDeadline);
		row.UpdatedAt = now;
		row.Version = checked(row.Version + 1);
		await context.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
	}

	public async ValueTask CommitAsync(TLease lease, TJob? job, CancellationToken token)
	{
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockScheduleAsync(context, lease.Owner, lease.ScheduleId, token);
		var row = await RequireLeaseAsync(lease, token);
		var definition = codec.Describe(lease.Definition);
		var now = clock.GetUtcNow();
		if (job is not null)
		{
			var current = jobs.Describe(job).Definition;
			if (current.Id != JobId(lease) || current.CommandId != CommandId(lease) || current.RequestedBy != lease.Owner ||
				current.OwnerModule != definition.OwnerModule || current.Kind != definition.Kind || current.ScopeKey != definition.ScopeKey ||
				current.ConcurrencyKey != definition.ConcurrencyKey || current.MaximumAttempts != definition.MaximumAttempts ||
				current.IdempotencyKey != IdempotencyKey(lease) || current.Deadline != lease.PreparationDeadline ||
				current.CreatedAt > now || current.ScheduledAt > now)
			{
				throw new ArgumentException("A recurring job must preserve its owned occurrence and preparation deadline.");
			}

			await events.AcceptAsync(context, lease, job, now, token);
		}
		else if (definition.MisfirePolicy != RecurringMisfirePolicy.Skip || now - lease.ScheduledAt < definition.Interval)
		{
			throw new ArgumentException("Only a missed occurrence with Skip policy can advance without a job.");
		}

		if (clock.GetUtcNow() >= lease.PreparationDeadline || row.LeaseUntil <= clock.GetUtcNow())
		{
			throw new DbUpdateConcurrencyException("The schedule preparation lease expired before dispatch committed.");
		}

		row.NextRunAt = Timestamp(definition.MisfirePolicy == RecurringMisfirePolicy.CatchUp ? lease.ScheduledAt + definition.Interval :
			NextOccurrence(lease.ScheduledAt, definition.Interval, now));
		row.LeaseId = null;
		row.LeaseUntil = null;
		row.UpdatedAt = now;
		row.Version = checked(row.Version + 1);
		await events.RecordAsync(context, lease, job is null ? RecurringOccurrenceOutcome.Skipped : RecurringOccurrenceOutcome.Dispatched,
			job is null ? null : jobs.Describe(job).Definition.Id, [], now, token);
		await context.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
	}

	public async ValueTask RecordWaitingAsync(TLease lease, ImmutableArray<string> missing, CancellationToken token)
	{
		if (missing.IsDefaultOrEmpty || missing.Length > 16)
		{
			throw new ArgumentException("A dependency wait requires the missing declared dependency kinds.");
		}

		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockScheduleAsync(context, lease.Owner, lease.ScheduleId, token);
		_ = await RequireLeaseAsync(lease, token);
		await events.RecordAsync(context, lease, RecurringOccurrenceOutcome.WaitingForDependencies, null, missing, clock.GetUtcNow(), token);
		await context.SaveChangesAsync(token);
		await transaction.CommitAsync(token);
	}

	public async ValueTask ReleaseAsync(TLease lease, CancellationToken token)
	{
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockScheduleAsync(context, lease.Owner, lease.ScheduleId, token);
		var row = await FindAsync(lease.Owner, lease.ScheduleId, token);
		if (row?.LeaseId == lease.LeaseId)
		{
			row.LeaseId = null;
			row.LeaseUntil = null;
			row.UpdatedAt = clock.GetUtcNow();
			row.Version = checked(row.Version + 1);
			await context.SaveChangesAsync(token);
		}

		await transaction.CommitAsync(token);
	}

	private async ValueTask<RecurringScheduleStateRow> RequireLeaseAsync(TLease lease, CancellationToken token)
	{
		var row = await FindAsync(lease.Owner, lease.ScheduleId, token);
		var now = clock.GetUtcNow();
		if (row is null || row.LeaseId != lease.LeaseId || row.LeaseUntil is null || row.LeaseUntil <= now ||
			row.NextRunAt != lease.ScheduledAt || row.DefinitionVersion != lease.ScheduleVersion ||
			now >= lease.PreparationDeadline || !await IsCurrentDefinitionAsync(lease.Definition, lease.Owner, token))
		{
			throw new DbUpdateConcurrencyException("The recurring occurrence lost its lease or current definition.");
		}

		return row;
	}

	private async ValueTask<bool> IsCurrentDefinitionAsync(TSchedule schedule, string owner, CancellationToken token)
	{
		var definition = codec.Describe(schedule);
		var now = clock.GetUtcNow();
		var latest = await context.Set<RecurringScheduleDefinitionRow>().AsNoTracking().Where(value => value.Owner == owner &&
			value.Id == definition.Id && value.AvailableAt <= now && value.PublishedAt <= now)
			.OrderByDescending(value => value.PublishedAt).ThenByDescending(value => value.Version).FirstOrDefaultAsync(token);
		if (latest is null || latest.RevokedAt is not null || latest.Version != definition.Version || !definition.Enabled ||
			definition.RequestedBy != owner || latest.ContentHash != definition.ContentHash)
		{
			return false;
		}

		if (PayloadSerializer.Hash(latest.Payload) != PayloadSerializer.Hash(codec.Encode(schedule)))
		{
			throw new InvalidDataException("The recurring schedule differs from its retained definition.");
		}

		return true;
	}

	private Task<RecurringScheduleStateRow?> FindAsync(string owner, string id, CancellationToken token)
	{
		return context.Set<RecurringScheduleStateRow>().SingleOrDefaultAsync(value => value.Owner == owner && value.ScheduleId == id, token);
	}

	private static DateTimeOffset Minimum(DateTimeOffset first, DateTimeOffset second)
	{
		return first < second ? first : second;
	}

	private static DateTimeOffset Timestamp(DateTimeOffset value)
	{
		return new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
	}

	private static DateTimeOffset NextOccurrence(DateTimeOffset previous, TimeSpan interval, DateTimeOffset now)
	{
		var elapsed = Math.Max(0, now.UtcTicks - previous.UtcTicks);
		return previous.AddTicks(checked(Math.Max(1, elapsed / interval.Ticks + 1) * interval.Ticks));
	}
}
