namespace Extensions.Jobs;

public interface IJobCodec<TJob> where TJob : class
{
	TJob Decode(string payload);
	string Encode(TJob job);
	JobDetails Describe(TJob job);
	bool SameDefinition(TJob first, TJob second);
	bool SameSubmission(TJob first, TJob second);
	IJobStateMachine<TJob> Restore(TJob job);
}
