namespace Extensions.Jobs;

public interface IJobDefinition
{
	Guid Id { get; }
	Guid CommandId { get; }
	string OwnerModule { get; }
	string Kind { get; }
	string IdempotencyKey { get; }
	string ConcurrencyKey { get; }
	string RequestedBy { get; }
	DateTimeOffset CreatedAt { get; }
	DateTimeOffset ScheduledAt { get; }
	DateTimeOffset Deadline { get; }
	int MaximumAttempts { get; }
	void Validate();
}
