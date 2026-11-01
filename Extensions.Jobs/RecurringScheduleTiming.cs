namespace Extensions.Jobs;

public static class RecurringScheduleTiming
{
	public const int MaximumSchedulesPerPass = 500;

	public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

	public static TimeSpan RenewalInterval => LeaseDuration / 3;

	public static DateTimeOffset NextRunAt(RecurringScheduleInfo definition, RecurringScheduleStateRow? state)
	{
		ArgumentNullException.ThrowIfNull(definition);
		if (state is null)
		{
			return definition.FirstRunAt;
		}

		return state.DefinitionVersion == definition.Version || state.NextRunAt >= definition.FirstRunAt
			? state.NextRunAt : definition.FirstRunAt;
	}
}
