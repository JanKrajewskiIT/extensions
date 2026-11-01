using Extensions.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Extensions.Jobs;

public static class WorkExecutor
{
	public static async ValueTask<T> RunAsync<TRequest, T>(IWorkAdmission<TRequest> admission, TRequest request,
		Func<CancellationToken, ValueTask<T>> action, ILogger logger, CancellationToken token)
	{
		var inherited = ExecutionBudgetScope.Current;
		if (inherited is { OwnsComputeReservation: true })
		{
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, inherited.Token);
			return await action(linked.Token);
		}

		using var parentCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, inherited?.Token ?? CancellationToken.None);
		var reservation = await admission.ReserveAsync(request, parentCancellation.Token);
		var executionBudget = inherited is null ? reservation.Budget : new CombinedBudget(reservation.Budget, inherited);
		using var budget = new ExecutionBudgetScope(executionBudget);
		using var fence = new OperationWriteScope(new CancellationWriteFence("shared-work-budget", reservation.Budget.Token));
		var outcome = WorkOutcome.Failed;
		Task<T>? pending = null;
		try
		{
			await executionBudget.TakeAsync(WorkAttemptKind.Handler, parentCancellation.Token);
			pending = action(reservation.Budget.Token).AsTask();
			var result = await pending.WaitAsync(reservation.Budget.Token);
			reservation.Budget.Token.ThrowIfCancellationRequested();
			outcome = WorkOutcome.Completed;
			return result;
		}
		catch (OperationCanceledException failure) when (!token.IsCancellationRequested && reservation.Budget.Token.IsCancellationRequested)
		{
			outcome = WorkOutcome.Deadline;
			throw new RetryableJobException("budget.attempt_deadline_exhausted", TimeSpan.FromSeconds(5), failure);
		}
		catch (OperationCanceledException)
		{
			outcome = WorkOutcome.Cancelled;
			throw;
		}
		finally
		{
			await reservation.StopAsync();
			if (pending is { IsCompleted: false })
			{
				reservation.Budget.Track(pending);
			}

			var outstanding = reservation.Pending;
			if (!outstanding.IsCompleted)
			{
				_ = SettleLateAsync(outstanding, reservation, outcome, logger);
			}
			else
			{
				await reservation.CompleteAsync(outcome);
			}
		}
	}

	private static async Task SettleLateAsync(Task pending, IWorkReservation reservation, WorkOutcome outcome, ILogger logger)
	{
		try
		{
			await pending;
			logger.LogWarning("Work {ReservationId} completed after invalidation; its result has no execution authority", reservation.Id);
		}
		catch (Exception failure)
		{
			logger.LogWarning("Late work {ReservationId} failed ({FailureType}); its reservation remains charged until settlement", reservation.Id, failure.GetType().Name);
		}

		await reservation.CompleteAsync(outcome);
	}

	private sealed class CombinedBudget(IExecutionBudget primary, IExecutionBudget parent) : IExecutionBudget
	{
		public bool OwnsComputeReservation => primary.OwnsComputeReservation;

		public CancellationToken Token => primary.Token;

		public async ValueTask TakeAsync(WorkAttemptKind kind, CancellationToken token)
		{
			await parent.TakeAsync(kind, token);
			await primary.TakeAsync(kind, token);
		}

		public void Track(Task pending)
		{
			primary.Track(pending);
			parent.Track(pending);
		}
	}
}
