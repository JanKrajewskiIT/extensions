namespace Extensions.Messaging;

public interface IIntegrationPublisher<TMessage> where TMessage : notnull
{
	ValueTask PublishAsync(string topic, TMessage message, CancellationToken cancellationToken = default);
}
