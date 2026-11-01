namespace Extensions.Messaging;

public interface IMessageProcessor<TMessage> where TMessage : notnull
{
	string MessageType { get; }
	ValueTask ProcessAsync(TMessage message, CancellationToken cancellationToken);
}
