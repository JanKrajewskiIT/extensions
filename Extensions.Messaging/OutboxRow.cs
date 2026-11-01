namespace Extensions.Messaging;

public sealed class OutboxRow
{
	public Guid Id { get; set; }
	public string Topic { get; set; } = "";
	public string StreamKey { get; set; } = "";
	public long StreamSequence { get; set; }
	public string Payload { get; set; } = "";
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset NextAttemptAt { get; set; }
	public DateTimeOffset? PublishedAt { get; set; }
	public DateTimeOffset? QuarantinedAt { get; set; }
	public string? FailureCode { get; set; }
	public int Attempts { get; set; }
	public Guid? LeaseId { get; set; }
	public DateTimeOffset? LeaseUntil { get; set; }
	public long Version { get; set; }
}
