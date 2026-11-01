namespace Extensions.Jobs;

public interface IWorkReservation : IAsyncDisposable
{
	Guid Id { get; }
	IExecutionBudget Budget { get; }
	Task Pending { get; }

	ValueTask StopAsync();

	ValueTask CompleteAsync(WorkOutcome outcome);
}
