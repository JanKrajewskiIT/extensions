namespace Extensions.Messaging;

public interface IMessageCodec<TMessage> where TMessage : notnull
{
	TMessage Decode(string payload);
	string Encode(TMessage message);
	MessageDescriptor Describe(TMessage message);
}
