namespace Extensions.Jobs;

public sealed class RecurringJobMaterializer<TSchedule, TLease, TInput, TJob>(
	Func<TSchedule, TLease, CancellationToken, ValueTask<TInput>> prepare,
	Func<TSchedule, TLease, TInput, DateTimeOffset, TJob> create, TimeProvider clock)
	where TLease : IRecurringOccurrence
{
	public async ValueTask<TJob> MaterializeAsync(TSchedule schedule, TLease lease, CancellationToken token)
	{
		var input = await prepare(schedule, lease, token);
		token.ThrowIfCancellationRequested();
		if (clock.GetUtcNow() >= lease.PreparationDeadline)
		{
			throw new TimeoutException("The recurring occurrence exhausted its preparation and execution window.");
		}

		return create(schedule, lease, input, clock.GetUtcNow());
	}
}
