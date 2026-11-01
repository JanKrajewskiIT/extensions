namespace Extensions.Jobs;

public sealed class RecurringScheduleStateRow
{
	public string Owner { get; set; } = "";

	public string ScheduleId { get; set; } = "";

	public string DefinitionVersion { get; set; } = "";

	public DateTimeOffset NextRunAt { get; set; }
	public DateTimeOffset? LeaseUntil { get; set; }
	public Guid? LeaseId { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public long Version { get; set; }
}
