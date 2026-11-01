using Extensions.Domain.Primitives;
using Microsoft.Extensions.Logging;

namespace Extensions.Jobs;

public sealed class SharedWorkBudgets<TRequest, TAllocation> : IWorkAdmission<TRequest> where TAllocation : class, IWorkAllocation
{
	private readonly ScopedJobOperation<IWorkBudgetStore<TRequest, TAllocation>> stores;

	private readonly TimeProvider clock;

	private readonly ILogger logger;

	public SharedWorkBudgets(ScopedJobOperation<IWorkBudgetStore<TRequest, TAllocation>> stores, TimeProvider clock, ILogger logger)
	{
		this.stores = stores;
		this.clock = clock;
		this.logger = logger;
	}

	private TimeProvider Clock => clock;

	private ILogger Logger => logger;

	public async ValueTask<IWorkReservation> ReserveAsync(TRequest request, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(request);
		var allocation = await stores.RunAsync((store, operation) => store.ReserveAsync(request, operation), token);
		return new Reservation(this, allocation, token);
	}

	private sealed class Reservation : IWorkReservation, IExecutionBudget
	{
		private readonly SharedWorkBudgets<TRequest, TAllocation> owner;

		private readonly TAllocation allocation;

		private readonly CancellationTokenSource cancellation;

		private readonly CancellationTokenSource deadline;

		private readonly object sync = new();

		private readonly HashSet<Task> pending = [];

		private volatile bool requiresResolution;

		private int disposed;

		public Reservation(SharedWorkBudgets<TRequest, TAllocation> owner, TAllocation allocation, CancellationToken token)
		{
			this.owner = owner;
			this.allocation = allocation;
			var remaining = allocation.Deadline - owner.Clock.GetUtcNow();
			deadline = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, owner.Clock);
			cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
		}

		public Guid Id => allocation.Id;

		public bool OwnsComputeReservation => true;

		public IExecutionBudget Budget => this;

		public Task Pending => DrainAsync();

		public CancellationToken Token => cancellation.Token;

		public async ValueTask TakeAsync(WorkAttemptKind kind, CancellationToken token)
		{
			Token.ThrowIfCancellationRequested();
			DomainGuard.Defined(kind, nameof(kind));
			using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, Token);
			await owner.stores.RunAsync((store, operation) => store.TakeAsync(allocation, operation), cancellation.Token);
		}

		public void Track(Task work)
		{
			lock (sync)
			{
				pending.RemoveWhere(value => value.IsCompleted);
				if (pending.Count >= 512)
				{
					requiresResolution = true;
					owner.Logger.LogError("Work {ReservationId} exceeded its outstanding operation budget and requires resolution", allocation.Id);
					return;
				}

				pending.Add(work);
			}
		}

		public async ValueTask StopAsync()
		{
			try
			{
				await cancellation.CancelAsync();
			}
			catch (Exception secondary)
			{
				owner.Logger.LogWarning("Work cancellation cleanup failed ({FailureType}); settlement continues", secondary.GetType().Name);
			}
		}

		public async ValueTask CompleteAsync(WorkOutcome outcome)
		{
			if (Interlocked.Exchange(ref disposed, 1) != 0)
			{
				return;
			}

			try
			{
				await StopAsync();
				if (requiresResolution)
				{
					owner.Logger.LogError("Work {ReservationId} requires explicit resolution of untracked work", allocation.Id);
					return;
				}

				await IsolatedWork.RunAsync(async () => await owner.stores.RunAsync((store, operation) => store.SettleAsync(allocation, outcome, operation), CancellationToken.None));
			}
			catch (Exception secondary)
			{
				owner.Logger.LogError("Work reservation {ReservationId} requires settlement ({FailureType})", allocation.Id, secondary.GetType().Name);
			}
			finally
			{
				cancellation.Dispose();
				deadline.Dispose();
			}
		}

		public ValueTask DisposeAsync()
		{
			return CompleteAsync(WorkOutcome.Failed);
		}

		private async Task DrainAsync()
		{
			while (true)
			{
				Task[] outstanding;
				lock (sync)
				{
					outstanding = [.. pending.Where(value => !value.IsCompleted)];
				}

				if (outstanding.Length == 0)
				{
					return;
				}

				try
				{
					await Task.WhenAll(outstanding);
				}
				catch (Exception failure)
				{
					owner.Logger.LogWarning("Outstanding work {ReservationId} ended with {FailureType}", allocation.Id, failure.GetType().Name);
				}
			}
		}
	}
}
