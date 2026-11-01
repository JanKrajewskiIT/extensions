namespace Extensions.Jobs;

public class RetryableJobException(string code, TimeSpan retryAfter, Exception? innerException = null)
	: Exception(code, innerException)
{
	public string Code { get; } = code;

	public TimeSpan RetryAfter { get; } = retryAfter;
}
