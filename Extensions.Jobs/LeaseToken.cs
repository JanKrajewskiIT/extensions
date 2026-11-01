namespace Extensions.Jobs;

public sealed record LeaseToken(Guid AttemptId, string Owner, long Fence, DateTimeOffset AcquiredAt);
