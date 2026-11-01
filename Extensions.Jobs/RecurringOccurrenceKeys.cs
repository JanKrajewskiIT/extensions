using Extensions.Domain.Primitives;

namespace Extensions.Jobs;

public static class RecurringOccurrenceKeys
{
	public static string OccurrenceKey(string id, string version, string hash)
	{
		return ContentIdentity.Create("recurring-occurrence", id, version, hash).ToString("N");
	}

	public static Guid CommandId(IRecurringOccurrence lease)
	{
		return ContentIdentity.Create("recurring-job-command", lease.Owner,
		lease.ScheduleId, lease.ScheduleVersion, ContentIdentity.Timestamp(lease.ScheduledAt));
	}

	public static Guid JobId(IRecurringOccurrence lease)
	{
		return ContentIdentity.Create("recurring-job-instance", lease.Owner,
		lease.ScheduleId, lease.ScheduleVersion, ContentIdentity.Timestamp(lease.ScheduledAt));
	}

	public static string IdempotencyKey(string prefix, IRecurringOccurrence lease)
	{
		return $"{prefix}:{ContentIdentity.Timestamp(lease.ScheduledAt)}";
	}
}
