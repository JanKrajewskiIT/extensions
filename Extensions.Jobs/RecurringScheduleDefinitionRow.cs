namespace Extensions.Jobs;

public sealed class RecurringScheduleDefinitionRow
{
	public string Owner { get; set; } = "";

	public string Id { get; set; } = "";

	public string Version { get; set; } = "";

	public string ContentHash { get; set; } = "";

	public string Payload { get; set; } = "";

	public DateTimeOffset PublishedAt { get; set; }
	public DateTimeOffset AvailableAt { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
}
