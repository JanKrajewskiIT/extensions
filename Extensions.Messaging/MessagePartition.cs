namespace Extensions.Messaging;

public sealed record MessagePartition(string Topic, int Partition);
