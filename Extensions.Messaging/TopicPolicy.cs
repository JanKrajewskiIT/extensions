using System.Collections.Immutable;

namespace Extensions.Messaging;

public sealed record TopicPolicy(
	string Name,
	string Owner,
	MessageKind Kind,
	ImmutableArray<string> MessageTypes,
	int PartitionCount,
	int ReplicationFactor,
	int MinimumInSyncReplicas,
	TimeSpan Retention,
	string PartitionKeyVersion,
	bool RequiresContinuity,
	ImmutableArray<string> ConsumerGroups,
	OffsetRecovery OffsetRecovery);
