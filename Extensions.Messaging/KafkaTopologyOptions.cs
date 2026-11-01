namespace Extensions.Messaging;

public sealed class KafkaTopologyOptions
{
	public bool Enabled { get; set; }
	public bool CreateMissing { get; set; }
	public int Partitions { get; set; }
	public short ReplicationFactor { get; set; }
	public int MinimumInSyncReplicas { get; set; }
	public TimeSpan Retention { get; set; }
	public long RetentionBytesPerPartition { get; set; }
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
	public bool IsValid() => !Enabled || Partitions is > 0 and <= 4096 && ReplicationFactor > 0 &&
		MinimumInSyncReplicas > 0 && MinimumInSyncReplicas <= ReplicationFactor && Retention > TimeSpan.Zero &&
		RetentionBytesPerPartition > 0 && RequestTimeout > TimeSpan.Zero && RequestTimeout <= TimeSpan.FromMinutes(2);
}
