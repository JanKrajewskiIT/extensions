namespace Extensions.Jobs;

public interface IRecurringScheduleCodec<TSchedule, TLease> where TSchedule : class
{
	TSchedule Decode(string payload);
	string Encode(TSchedule schedule);
	RecurringScheduleInfo Describe(TSchedule schedule);
	void Validate(TSchedule schedule);
	TLease CreateLease(TSchedule schedule, string owner, DateTimeOffset scheduledAt, Guid leaseId, DateTimeOffset deadline);
}
