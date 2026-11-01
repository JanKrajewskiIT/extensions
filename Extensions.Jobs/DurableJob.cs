using Extensions.Domain.Primitives;

namespace Extensions.Jobs;

public class DurableJob<TDefinition, TCheckpoint, TArtifact>
	where TDefinition : IJobDefinition
	where TCheckpoint : class, IJobCheckpoint
	where TArtifact : class
{
	public DurableJob(TDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(definition);
		definition.Validate();

		Definition = definition;
		NextAttemptAt = definition.ScheduledAt.ToUniversalTime();
	}

	public DurableJob(JobSnapshot<TDefinition, TCheckpoint, TArtifact> snapshot) : this((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Definition)
	{
		DomainGuard.Defined(snapshot.Status, nameof(snapshot.Status));
		ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Version);
		ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Fence);
		if (snapshot.Attempts < 0 || snapshot.Attempts > snapshot.Definition.MaximumAttempts ||
			snapshot.Fence < snapshot.Attempts ||
			(snapshot.Status == JobStatus.Running) != (snapshot.Lease is not null && snapshot.LeaseUntil is not null) ||
			(snapshot.Status != JobStatus.Running && (snapshot.Lease is not null || snapshot.LeaseUntil is not null)) ||
			(snapshot.Lease is not null && (snapshot.Lease.Fence != snapshot.Fence ||
				snapshot.Lease.AcquiredAt >= snapshot.LeaseUntil)) ||
			(snapshot.Status == JobStatus.Completed) != (snapshot.Result is not null) ||
			(snapshot.Status == JobStatus.Cancelled && !snapshot.CancellationRequested))
		{
			throw new ArgumentException("Inconsistent persisted job state.", nameof(snapshot));
		}

		Status = snapshot.Status;
		Attempts = snapshot.Attempts;
		Lease = snapshot.Lease;
		LeaseUntil = snapshot.LeaseUntil;
		NextAttemptAt = snapshot.NextAttemptAt;
		CancellationRequested = snapshot.CancellationRequested;
		Checkpoint = snapshot.Checkpoint;
		Result = snapshot.Result;
		FailureCode = snapshot.FailureCode;
		Fence = snapshot.Fence;
		Version = snapshot.Version;
	}

	public TDefinition Definition { get; }
	public JobStatus Status { get; private set; } = JobStatus.Pending;

	public int Attempts { get; private set; }
	public LeaseToken? Lease { get; private set; }
	public DateTimeOffset? LeaseUntil { get; private set; }
	public DateTimeOffset NextAttemptAt { get; private set; }
	public bool CancellationRequested { get; private set; }
	public TCheckpoint? Checkpoint { get; private set; }
	public TArtifact? Result { get; private set; }
	public string? FailureCode { get; private set; }
	public long Fence { get; private set; }
	public long Version { get; private set; }

	public LeaseToken Acquire(Guid attemptId, string owner, DateTimeOffset now, TimeSpan duration)
	{
		DomainGuard.Identity(attemptId, nameof(attemptId));
		DomainGuard.Required(owner, nameof(owner));
		if (duration <= TimeSpan.Zero || now < NextAttemptAt || now >= Definition.Deadline ||
			CancellationRequested || Attempts >= Definition.MaximumAttempts ||
			Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled ||
			(Status == JobStatus.Running && LeaseUntil > now))
		{
			throw new InvalidOperationException("The job is not eligible for a lease.");
		}

		var until = now.Add(duration);
		LeaseUntil = (until < Definition.Deadline ? until : Definition.Deadline).ToUniversalTime();
		Fence = checked(Fence + 1);
		var lease = new LeaseToken(attemptId, owner, Fence, now.ToUniversalTime());
		Lease = lease;
		Attempts++;
		Status = JobStatus.Running;
		Version++;
		return lease;
	}

	public void Renew(LeaseToken token, DateTimeOffset now, TimeSpan duration)
	{
		RequireLease(token, now);
		if (duration <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(duration));
		}

		var until = now.Add(duration);
		LeaseUntil = (until < Definition.Deadline ? until : Definition.Deadline).ToUniversalTime();
		Version++;
	}

	public void SaveCheckpoint(LeaseToken token, TCheckpoint checkpoint, DateTimeOffset now)
	{
		RequireLease(token, now);
		ArgumentNullException.ThrowIfNull(checkpoint);
		checkpoint.Validate();
		if (checkpoint.SavedAt > now || checkpoint.SavedAt < Definition.CreatedAt ||
			(Checkpoint is not null && checkpoint.SavedAt < Checkpoint.SavedAt))
		{
			throw new ArgumentException("Invalid checkpoint chronology.", nameof(checkpoint));
		}

		Checkpoint = checkpoint;
		Version++;
	}

	public void Complete(LeaseToken token, TArtifact result, DateTimeOffset now)
	{
		RequireLease(token, now);
		ArgumentNullException.ThrowIfNull(result);
		if (CancellationRequested)
		{
			throw new InvalidOperationException("A cancelled operation cannot publish a successful result.");
		}

		Result = result;
		Status = JobStatus.Completed;
		ReleaseLease();
	}

	public void Fail(LeaseToken token, string code, JobFailureKind kind, DateTimeOffset now,
		DateTimeOffset? retryAt)
	{
		RequireLease(token, now);
		DomainGuard.Required(code, nameof(code));
		DomainGuard.Defined(kind, nameof(kind));
		if (retryAt is not null && (kind != JobFailureKind.Transient || retryAt <= now || retryAt >= Definition.Deadline))
		{
			throw new ArgumentException("Retry must be transient, in the future and before the deadline.", nameof(retryAt));
		}

		FailureCode = code;
		if (CancellationRequested)
		{
			Status = JobStatus.Cancelled;
		}
		else if (retryAt is { } next && Attempts < Definition.MaximumAttempts)
		{
			NextAttemptAt = next.ToUniversalTime();
			Status = JobStatus.RetryScheduled;
		}
		else
		{
			Status = JobStatus.Failed;
		}

		ReleaseLease();
	}

	public void RequestCancellation(DateTimeOffset now)
	{
		if (Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
		{
			return;
		}

		CancellationRequested = true;
		if (Status != JobStatus.Running || LeaseUntil <= now)
		{
			Status = JobStatus.Cancelled;
			ReleaseLease();
		}
		else
		{
			Version++;
		}
	}

	public void DeferForCapacity(LeaseToken token, string code, DateTimeOffset now, DateTimeOffset retryAt)
	{
		RequireLease(token, now);
		if (retryAt <= now || retryAt >= Definition.Deadline)
		{
			throw new ArgumentOutOfRangeException(nameof(retryAt));
		}

		FailureCode = DomainGuard.Required(code, nameof(code));
		Attempts--;
		NextAttemptAt = retryAt;
		Status = CancellationRequested ? JobStatus.Cancelled : JobStatus.RetryScheduled;
		ReleaseLease();
	}

	public void AcknowledgeCancellation(LeaseToken token, DateTimeOffset now)
	{
		RequireLease(token, now);
		if (!CancellationRequested)
		{
			throw new InvalidOperationException("Cancellation was not requested.");
		}

		Status = JobStatus.Cancelled;
		ReleaseLease();
	}

	public void Expire(DateTimeOffset now)
	{
		if (now < Definition.Deadline || Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
		{
			throw new InvalidOperationException("The job cannot expire.");
		}

		Status = CancellationRequested ? JobStatus.Cancelled : JobStatus.Failed;
		FailureCode = "job.deadline_exceeded";
		ReleaseLease();
	}

	public void RecoverExpiredLease(DateTimeOffset now, DateTimeOffset retryAt)
	{
		if (Status != JobStatus.Running || LeaseUntil is null || LeaseUntil > now)
		{
			throw new InvalidOperationException("Only an expired running lease can be recovered.");
		}

		if (CancellationRequested)
		{
			Status = JobStatus.Cancelled;
		}
		else if (now >= Definition.Deadline || Attempts >= Definition.MaximumAttempts)
		{
			Status = JobStatus.Failed;
			FailureCode = now >= Definition.Deadline ? "job.deadline_exceeded" : "job.attempts_exhausted";
		}
		else
		{
			if (retryAt < now || retryAt >= Definition.Deadline)
			{
				throw new ArgumentOutOfRangeException(nameof(retryAt));
			}

			Status = JobStatus.RetryScheduled;
			FailureCode = "job.lease_expired";
			NextAttemptAt = retryAt.ToUniversalTime();
		}

		ReleaseLease();
	}

	public virtual JobSnapshot<TDefinition, TCheckpoint, TArtifact> Snapshot()
	{
		return new(Definition, Status, Attempts, Lease, LeaseUntil, NextAttemptAt,
		CancellationRequested, Checkpoint, Result, FailureCode, Fence, Version);
	}

	private void RequireLease(LeaseToken token, DateTimeOffset now)
	{
		ArgumentNullException.ThrowIfNull(token);
		if (Status != JobStatus.Running || Lease != token || now < token.AcquiredAt ||
			LeaseUntil <= now || now >= Definition.Deadline)
		{
			throw new InvalidOperationException("The lease is stale or expired.");
		}
	}

	private void ReleaseLease()
	{
		Lease = null;
		LeaseUntil = null;
		Version++;
	}
}
