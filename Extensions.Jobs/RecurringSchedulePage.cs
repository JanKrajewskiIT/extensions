using System.Collections.Immutable;

namespace Extensions.Jobs;

public sealed record RecurringSchedulePage<TSchedule>(ImmutableArray<TSchedule> Items, RecurringScheduleCursor? Next);
