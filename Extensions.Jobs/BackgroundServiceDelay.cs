namespace Extensions.Jobs;

public static class BackgroundServiceDelay
{
	public static async ValueTask<bool> WaitAsync(TimeSpan delay, TimeProvider clock, CancellationToken stoppingToken)
	{
		try
		{
			await Task.Delay(delay, clock, stoppingToken);
			return true;
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			return false;
		}
	}
}
