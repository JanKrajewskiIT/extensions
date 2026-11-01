using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Jobs;

public sealed class ScopedJobOperation<TService>(IServiceScopeFactory scopes) where TService : notnull
{
	public async ValueTask<TResult> RunAsync<TResult>(Func<TService, CancellationToken, ValueTask<TResult>> operation, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(operation);
		await using var scope = scopes.CreateAsyncScope();
		return await operation(scope.ServiceProvider.GetRequiredService<TService>(), token);
	}

	public async ValueTask RunAsync(Func<TService, CancellationToken, ValueTask> operation, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(operation);
		await using var scope = scopes.CreateAsyncScope();
		await operation(scope.ServiceProvider.GetRequiredService<TService>(), token);
	}
}
