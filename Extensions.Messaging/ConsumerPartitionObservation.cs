namespace Extensions.Messaging;

public sealed record ConsumerPartitionObservation(string SubscriptionName, string Topic, string Consumer, int Partition,
	DateTimeOffset ObservedAt, long CommittedOffset, long EarliestOffset, long LatestOffset, DateTimeOffset? MessageAt);
