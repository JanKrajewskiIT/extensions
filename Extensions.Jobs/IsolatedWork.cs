namespace Extensions.Jobs;

public static class IsolatedWork
{
	public static Task RunAsync(Func<Task> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		if (ExecutionContext.IsFlowSuppressed())
		{
			return Task.Run(action, CancellationToken.None);
		}

		using var suppression = ExecutionContext.SuppressFlow();
		return Task.Run(action, CancellationToken.None);
	}
}
