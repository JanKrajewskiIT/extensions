namespace Extensions.Messaging;

public sealed record MessageDescriptor(Guid MessageId, string MessageType, string PartitionKey, string? TraceParent);
