namespace Extensions.Jobs;

public interface IWorkAllocation
{
	Guid Id { get; }
	Guid OperationId { get; }
	string Owner { get; }
	DateTimeOffset StartedAt { get; }
	DateTimeOffset Deadline { get; }
	long ReservedMilliseconds { get; }
}
