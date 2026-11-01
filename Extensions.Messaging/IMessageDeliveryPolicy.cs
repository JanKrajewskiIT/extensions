namespace Extensions.Messaging;

public interface IMessageDeliveryPolicy<TMessage> where TMessage : notnull
{
	bool Accepts(TMessage message, KafkaSubscriptionOptions subscription);
	IMessageProcessingBudget BeginBudget(KafkaSubscriptionOptions subscription, CancellationToken cancellationToken);
	KafkaDeliveryOutcome? Classify(Exception failure);
}
