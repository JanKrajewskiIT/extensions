using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public interface IRecurringPersistenceEvents<TLease, TJob> where TJob : class
{
	ValueTask AcceptAsync(DbContext context, TLease lease, TJob job, DateTimeOffset now, CancellationToken token);
	ValueTask RecordAsync(DbContext context, TLease lease, RecurringOccurrenceOutcome outcome, Guid? jobId,
		ImmutableArray<string> missing, DateTimeOffset now, CancellationToken token);
}
