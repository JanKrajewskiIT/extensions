namespace Extensions.Messaging;

public interface IMessageQuarantine
{
	ValueTask<IReadOnlyCollection<MessagePartition>> ReadBlockedAsync(string consumer,
		IReadOnlyCollection<MessagePartition> partitions, CancellationToken cancellationToken);
	ValueTask StoreAsync(QuarantineRequest request, KafkaSubscriptionOptions subscription, CancellationToken cancellationToken);
}
