using Extensions.Infrastructure.Database;
using Extensions.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public sealed class EfJobStore<TContext, TJob>(TContext context, IJobCodec<TJob> codec,
	IJobPersistenceEvents<TJob> events, TimeProvider clock) : IJobPersistence<TJob>
	where TContext : DbContext where TJob : class
{
	public static ValueTask LockJobAsync(DbContext context, Guid id, CancellationToken token)
	{
		return context.LockAsync(DatabaseLockKeys.Create("job-state", id.ToString("N")), token);
	}


	public async ValueTask<TJob?> FindAsync(Guid jobId, string scopeKey, CancellationToken token)
	{
		var payload = await context.Set<JobRow>().AsNoTracking().Where(row => row.Id == jobId && row.ScopeKey == scopeKey)
			.Select(row => row.Payload).SingleOrDefaultAsync(token);
		return payload is null ? null : codec.Decode(payload);
	}

	public ValueTask<bool> SaveAsync(TJob job, long expectedVersion, CancellationToken token)
	{
		return SaveWithOutboxAsync(job, expectedVersion, [], token);
	}

	public async ValueTask<bool> SaveWithOutboxAsync(TJob job, long expectedVersion,
		IReadOnlyCollection<JobOutboxMessage> outbox, CancellationToken token)
	{
		_ = codec.Restore(job);
		var current = codec.Describe(job);
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		await LockJobAsync(context, current.Definition.Id, token);
		var row = await context.Set<JobRow>().SingleOrDefaultAsync(value => value.Id == current.Definition.Id &&
			value.ScopeKey == current.Definition.ScopeKey, token);
		if (row is null || row.Version != expectedVersion || current.Version <= expectedVersion)
		{
			return false;
		}

		var previous = codec.Decode(row.Payload);
		if (!codec.SameDefinition(previous, job) || codec.Describe(previous).Status is JobStatus.Completed or JobStatus.Cancelled or JobStatus.Failed)
		{
			throw new InvalidOperationException("Job definitions and terminal results are immutable.");
		}

		Update(row, job);
		await events.StageAttemptAsync(context, previous, job, clock.GetUtcNow(), token);
		foreach (var message in outbox)
		{
			await MessageTransactions.StageAsync(context, message.Topic, message.MessageId, message.PartitionKey,
				message.Payload, message.NextAttemptAt, token);
		}

		await events.StageFinishedAsync(context, job, clock.GetUtcNow(), token);
		try
		{
			await context.SaveChangesAsync(token);
		}
		catch (DbUpdateConcurrencyException)
		{
			context.ChangeTracker.Clear();
			return false;
		}

		await transaction.CommitAsync(token);
		return true;
	}

	public async ValueTask<MessageAcceptance> AcceptAsync(JobInboxReceipt receipt, TJob job, CancellationToken token)
	{
		await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
		var result = await AcceptInTransactionAsync(receipt, job, token);
		if (result != MessageAcceptance.ConflictingDuplicate)
		{
			await transaction.CommitAsync(token);
		}

		return result;
	}

	public async ValueTask<MessageAcceptance> AcceptInTransactionAsync(JobInboxReceipt receipt, TJob job, CancellationToken token)
	{
		_ = codec.Restore(job);
		var current = codec.Describe(job);
		var definition = current.Definition;
		if (current.Status != JobStatus.Pending || current.Version != 0 || current.Attempts != 0)
		{
			throw new ArgumentException("New durable jobs must be unleased pending commands.", nameof(job));
		}

		if (receipt.ScopeKey != definition.ScopeKey || receipt.MessageId != definition.CommandId)
		{
			throw new ArgumentException("The inbox receipt and job command scopes must match.", nameof(receipt));
		}

		if (context.Database.CurrentTransaction is null)
		{
			throw new InvalidOperationException("Job acceptance requires an explicit transaction.");
		}

		var result = await MessageTransactions.AcceptAsync(context, receipt.Consumer, receipt.ScopeKey, receipt.MessageId,
			receipt.PayloadHash, receipt.ProcessedAt, token);
		if (result != MessageAcceptance.Accepted)
		{
			return result;
		}

		await context.LockAsync(DatabaseLockKeys.Create("job", definition.RequestedBy, definition.OwnerModule,
			definition.ScopeKey, definition.IdempotencyKey), token);
		var existing = await context.Set<JobRow>().AsNoTracking().SingleOrDefaultAsync(row =>
			row.OwnerSubject == definition.RequestedBy && row.OwnerModule == definition.OwnerModule && row.ScopeKey == definition.ScopeKey &&
			row.IdempotencyKey == definition.IdempotencyKey, token);
		if (existing is not null)
		{
			if (!codec.SameSubmission(codec.Decode(existing.Payload), job))
			{
				return MessageAcceptance.ConflictingDuplicate;
			}

			await context.SaveChangesAsync(token);
			return MessageAcceptance.Duplicate;
		}

		context.Set<JobRow>().Add(ToRow(job));
		await events.StageAttemptAsync(context, job, job, receipt.ProcessedAt, token);
		await context.SaveChangesAsync(token);
		return result;
	}

	public async ValueTask<TJob?> ClaimAsync(JobRunnerOptions options, CancellationToken token)
	{
		var now = clock.GetUtcNow();
		var jobs = context.Set<JobRow>();
		var candidates = await jobs.AsNoTracking().Where(row => options.OwnerModules.Contains(row.OwnerModule) &&
			(options.IncludedKinds.Length == 0 || options.IncludedKinds.Contains(row.Kind)) && !options.ExcludedKinds.Contains(row.Kind) &&
			((row.Status == (int)JobStatus.Pending || row.Status == (int)JobStatus.RetryScheduled) && row.NextAttemptAt <= now ||
				row.Status == (int)JobStatus.Running && row.LeaseUntil <= now))
			.OrderBy(row => row.NextAttemptAt).ThenBy(row => row.Id).Take(32).ToListAsync(token);
		foreach (var candidate in candidates)
		{
			await using var transaction = await DatabaseTransactions.BeginAsync(context, token);
			await context.LockAsync(DatabaseLockKeys.Create("job-concurrency", candidate.ScopeKey, candidate.ConcurrencyKey), token);
			await LockJobAsync(context, candidate.Id, token);
			now = clock.GetUtcNow();
			var row = await jobs.SingleAsync(value => value.Id == candidate.Id, token);
			if (row.Version != candidate.Version || await jobs.AnyAsync(other => other.Id != row.Id &&
				other.ScopeKey == row.ScopeKey && other.ConcurrencyKey == row.ConcurrencyKey &&
				other.Status == (int)JobStatus.Running && other.LeaseUntil > now, token))
			{
				context.ChangeTracker.Clear();
				continue;
			}

			var previous = codec.Decode(row.Payload);
			var job = codec.Restore(previous);
			if (job.Details.Status == JobStatus.Running)
			{
				job.RecoverExpiredLease(now, now);
				await events.StageAttemptAsync(context, previous, job.Snapshot(), now, token);
				previous = job.Snapshot();
			}

			if (job.Details.Status is JobStatus.Pending or JobStatus.RetryScheduled && now >= job.Details.Definition.Deadline)
			{
				job.Expire(now);
			}

			if (job.Details.Status is JobStatus.Pending or JobStatus.RetryScheduled)
			{
				job.Acquire(Guid.NewGuid(), options.WorkerId, now, options.LeaseDuration);
			}

			var snapshot = job.Snapshot();
			await events.StageAttemptAsync(context, previous, snapshot, now, token);
			Update(row, snapshot);
			await events.StageFinishedAsync(context, snapshot, now, token);
			await context.SaveChangesAsync(token);
			await transaction.CommitAsync(token);
			context.ChangeTracker.Clear();
			if (job.Details.Status == JobStatus.Running)
			{
				return snapshot;
			}
		}

		return null;
	}

	public JobRow ToRow(TJob job)
	{
		return JobRows.Create(codec.Describe(job), codec.Encode(job));
	}

	private void Update(JobRow row, TJob job)
	{
		JobRows.Update(row, codec.Describe(job), codec.Encode(job));
	}

}
