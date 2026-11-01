namespace Extensions.Jobs;

public sealed record RecurringJobDependency(string Kind, TimeSpan MaximumAge);
