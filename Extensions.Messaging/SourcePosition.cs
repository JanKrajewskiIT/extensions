namespace Extensions.Messaging;

public sealed record SourcePosition(string Topic, int Partition, long Offset);
