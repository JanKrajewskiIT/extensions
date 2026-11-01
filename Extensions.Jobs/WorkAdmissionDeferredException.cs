namespace Extensions.Jobs;

public sealed class WorkAdmissionDeferredException(string code, TimeSpan retryAfter)
	: RetryableJobException(code, retryAfter)
{
}
