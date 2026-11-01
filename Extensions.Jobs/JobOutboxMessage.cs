namespace Extensions.Jobs;

public sealed record JobOutboxMessage(string Topic, Guid MessageId, string PartitionKey, string Payload, DateTimeOffset NextAttemptAt);
