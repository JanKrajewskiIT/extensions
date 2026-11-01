using System.Collections.Immutable;

namespace Extensions.Jobs;

public sealed record RecurringScheduleInfo(string Id, string Version, string ContentHash, bool Enabled, string OwnerModule,
	string Kind, string ScopeKey, string RequestedBy, string IdempotencyPrefix, string ConcurrencyKey, DateTimeOffset FirstRunAt,
	TimeSpan Interval, TimeSpan ExecutionWindow, int MaximumAttempts, RecurringMisfirePolicy MisfirePolicy,
	int Priority, int MaximumCatchUpRuns, ImmutableArray<RecurringJobDependency> Dependencies);
