namespace Extensions.Messaging;

public sealed class QuarantineRow
{
	public Guid Id { get; set; }
	public string Consumer { get; set; } = "";
	public string Topic { get; set; } = "";
	public int Partition { get; set; }
	public long Offset { get; set; }
	public string ReasonCode { get; set; } = "";
	public string Artifact { get; set; } = "";
	public DateTimeOffset QuarantinedAt { get; set; }
	public DateTimeOffset? ResolvedAt { get; set; }
	public bool BlocksScope { get; set; }
	public long Version { get; set; }
}
