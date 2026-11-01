namespace Extensions.Jobs;

public interface IWorkBudgetStore<in TRequest, TAllocation> where TAllocation : class, IWorkAllocation
{
	ValueTask<TAllocation> ReserveAsync(TRequest request, CancellationToken token);
	ValueTask TakeAsync(TAllocation allocation, CancellationToken token);
	ValueTask SettleAsync(TAllocation allocation, WorkOutcome outcome, CancellationToken token);
}
