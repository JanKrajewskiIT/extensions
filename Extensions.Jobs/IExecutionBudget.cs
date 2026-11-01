namespace Extensions.Jobs;

public interface IExecutionBudget
{
	bool OwnsComputeReservation { get; }
	CancellationToken Token { get; }

	ValueTask TakeAsync(WorkAttemptKind kind, CancellationToken token);

	void Track(Task pending);
}
