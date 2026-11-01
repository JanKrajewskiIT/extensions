namespace Extensions.Jobs;

public static class JobRows
{
	public static JobRow Create(JobDetails details, string payload)
	{
		var definition = details.Definition;
		var creationTicks = definition.CreatedAt.UtcTicks;
		var row = new JobRow
		{
			Id = definition.Id,
			CommandId = definition.CommandId,
			OwnerModule = definition.OwnerModule,
			Kind = definition.Kind,
			ScopeKey = definition.ScopeKey,
			IdempotencyKey = definition.IdempotencyKey,
			ConcurrencyKey = definition.ConcurrencyKey,
			OwnerSubject = definition.RequestedBy,
			CreatedAt = new(creationTicks - creationTicks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero),
			Deadline = definition.Deadline,
		};
		Update(row, details, payload);
		return row;
	}

	public static void Update(JobRow row, JobDetails details, string payload)
	{
		row.Status = (int)details.Status;
		row.NextAttemptAt = details.NextAttemptAt;
		row.LeaseUntil = details.LeaseUntil;
		row.Payload = payload;
		row.Version = details.Version;
	}
}
