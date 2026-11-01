namespace Extensions.Messaging;

public sealed class OutboxStreamRow
{
	public string StreamKey { get; set; } = "";
	public long Sequence { get; set; }
}
