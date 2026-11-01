namespace Extensions.Messaging;

public record OutboxRecord<TMessage>(Guid Id, string Owner, string StreamKey, long StreamSequence, TMessage Message,
	OutboxStatus Status, int Attempts, DateTimeOffset NextAttemptAt, string? LeaseOwner, long Fence,
	DateTimeOffset? LeaseUntil, long Version, string Topic);
