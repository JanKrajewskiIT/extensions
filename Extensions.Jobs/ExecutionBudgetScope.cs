namespace Extensions.Jobs;

public sealed class ExecutionBudgetScope : IDisposable
{
	public const int DefaultMaximumAttempts = 1000;

	private static readonly AsyncLocal<IExecutionBudget?> ambient = new();

	private readonly IExecutionBudget? previous;

	private readonly IExecutionBudget budget;

	public ExecutionBudgetScope(IExecutionBudget budget)
	{
		this.budget = budget;
		previous = ambient.Value;
		ambient.Value = budget;
	}

	public static TimeSpan MaximumRequestDuration { get; } = TimeSpan.FromMinutes(5);

	public static IExecutionBudget? Current => ambient.Value;

	public static async Task<T> DependencyAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token)
	{
		var budget = Current;
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, budget?.Token ?? CancellationToken.None);
		if (budget is not null)
		{
			await budget.TakeAsync(WorkAttemptKind.Dependency, cancellation.Token);
		}

		var pending = action(cancellation.Token);
		budget?.Track(pending);
		return await pending;
	}

	public void Dispose()
	{
		if (ambient.Value != budget)
		{
			throw new InvalidOperationException("Execution budgets must be disposed in ownership order.");
		}

		ambient.Value = previous;
	}
}
