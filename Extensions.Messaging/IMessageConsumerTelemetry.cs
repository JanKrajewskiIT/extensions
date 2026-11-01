namespace Extensions.Messaging;

public interface IMessageConsumerTelemetry
{
	ValueTask ObserveAsync(ConsumerPartitionObservation observation, KafkaSubscriptionOptions subscription, CancellationToken cancellationToken);
}
