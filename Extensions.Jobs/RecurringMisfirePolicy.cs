namespace Extensions.Jobs;

public enum RecurringMisfirePolicy
{
	Skip,
	RunOnce,
	CatchUp,
}
