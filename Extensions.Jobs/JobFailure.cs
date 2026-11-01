namespace Extensions.Jobs;

public sealed record JobFailure(string Code, JobFailureKind Kind, TimeSpan? RetryAfter = null, bool Deferred = false);
