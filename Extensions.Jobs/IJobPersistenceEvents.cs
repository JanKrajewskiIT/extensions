using Microsoft.EntityFrameworkCore;

namespace Extensions.Jobs;

public interface IJobPersistenceEvents<TJob> where TJob : class
{
	ValueTask StageAttemptAsync(DbContext context, TJob previous, TJob current, DateTimeOffset now, CancellationToken token);
	ValueTask StageFinishedAsync(DbContext context, TJob current, DateTimeOffset now, CancellationToken token);
}
