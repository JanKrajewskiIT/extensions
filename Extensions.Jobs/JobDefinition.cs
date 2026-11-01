using Extensions.Domain.Primitives;

namespace Extensions.Jobs;

public record JobDefinition<TScope, TTrace, TArtifact>(
	Guid Id,
	Guid CommandId,
	string OwnerModule,
	string Kind,
	string IdempotencyKey,
	TScope Scope,
	TTrace Trace,
	TArtifact Input,
	DateTimeOffset CreatedAt,
	DateTimeOffset ScheduledAt,
	DateTimeOffset Deadline,
	int MaximumAttempts,
	string ConcurrencyKey,
	string RequestedBy) : IJobDefinition
{
	public void Validate()
	{
		DomainGuard.Identity(Id, nameof(Id));
		DomainGuard.Identity(CommandId, nameof(CommandId));
		DomainGuard.Required(OwnerModule, nameof(OwnerModule));
		DomainGuard.Required(Kind, nameof(Kind));
		DomainGuard.Required(IdempotencyKey, nameof(IdempotencyKey));
		DomainGuard.Required(ConcurrencyKey, nameof(ConcurrencyKey));
		DomainGuard.Required(RequestedBy, nameof(RequestedBy));
		DomainGuard.Positive(MaximumAttempts, nameof(MaximumAttempts));
		ArgumentNullException.ThrowIfNull(Scope);
		ArgumentNullException.ThrowIfNull(Trace);
		ArgumentNullException.ThrowIfNull(Input);
		if (ScheduledAt < CreatedAt || Deadline <= ScheduledAt)
		{
			throw new ArgumentException("Invalid job schedule or deadline.", "definition");
		}
	}
}
