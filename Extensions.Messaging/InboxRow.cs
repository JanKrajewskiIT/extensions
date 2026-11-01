namespace Extensions.Messaging;

public sealed class InboxRow
{
	public string Consumer { get; set; } = "";
	public string ScopeKey { get; set; } = "";
	public Guid MessageId { get; set; }
	public string PayloadHash { get; set; } = "";
	public DateTimeOffset ProcessedAt { get; set; }
}
