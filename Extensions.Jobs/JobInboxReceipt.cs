namespace Extensions.Jobs;

public sealed record JobInboxReceipt(string Consumer, string ScopeKey, Guid MessageId, string PayloadHash, DateTimeOffset ProcessedAt);
