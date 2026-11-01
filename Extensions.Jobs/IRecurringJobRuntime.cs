using System.Collections.Immutable;

namespace Extensions.Jobs;

public interface IRecurringJobRuntime<TSchedule, TLease, TJob> where TSchedule : class where TLease : class, IRecurringOccurrence where TJob : class
{
	ValueTask<RecurringSchedulePage<TSchedule>> ReadPageAsync(RecurringScheduleCursor? after, CancellationToken token);
	RecurringScheduleInfo Describe(TSchedule schedule);
	ValueTask<TLease?> AcquireAsync(TSchedule schedule, string owner, CancellationToken token);
	ValueTask RenewAsync(TLease lease, CancellationToken token);
	ValueTask<ImmutableArray<string>> MissingDependenciesAsync(TSchedule schedule, string owner, DateTimeOffset scheduledAt, CancellationToken token);
	ValueTask RecordWaitingAsync(TLease lease, ImmutableArray<string> missing, CancellationToken token);
	ValueTask<TJob> MaterializeAsync(TSchedule schedule, TLease lease, CancellationToken token);
	ValueTask CommitAsync(TLease lease, TJob? job, CancellationToken token);
	ValueTask ReleaseAsync(TLease lease, CancellationToken token);
}
