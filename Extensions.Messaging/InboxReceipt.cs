namespace Extensions.Messaging;

public record InboxReceipt<TKey, THash>(TKey Key, THash PayloadHash, DateTimeOffset ProcessedAt);
