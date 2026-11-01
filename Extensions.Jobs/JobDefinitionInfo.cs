namespace Extensions.Jobs;

public sealed record JobDefinitionInfo(Guid Id, Guid CommandId, string OwnerModule, string Kind, string ScopeKey,
	string IdempotencyKey, string ConcurrencyKey, string RequestedBy, string InputHash,
	DateTimeOffset CreatedAt, DateTimeOffset ScheduledAt, DateTimeOffset Deadline, int MaximumAttempts);
