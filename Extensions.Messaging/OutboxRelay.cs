using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class OutboxRelay<TContext, TMessage>(IServiceScopeFactory scopes, IIntegrationPublisher<TMessage> publisher,
	IMessageCodec<TMessage> codec,
	TimeProvider clock, IOptions<OutboxOptions> options, ILogger<OutboxRelay<TContext, TMessage>> logger) : BackgroundService
	where TContext : DbContext
	where TMessage : notnull
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var delay = TimeSpan.Zero;
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, clock, stoppingToken);
				}

				delay = await PublishNextAsync(stoppingToken) ? TimeSpan.Zero : options.Value.PollInterval;
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError("Outbox relay iteration failed for {Context} ({FailureType})", typeof(TContext).Name, exception.GetType().Name);
				delay = options.Value.RetryDelay;
			}
		}
	}

	private async Task<bool> PublishNextAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<TContext>();
		var now = clock.GetUtcNow();
		var candidate = await context.Set<OutboxRow>().AsNoTracking().Where(row => row.PublishedAt == null &&
			row.QuarantinedAt == null && row.NextAttemptAt <= now && (row.LeaseUntil == null || row.LeaseUntil <= now) &&
			!context.Set<OutboxRow>().Any(previous => previous.StreamKey == row.StreamKey &&
				previous.StreamSequence < row.StreamSequence && previous.PublishedAt == null))
			.OrderBy(row => row.CreatedAt)
			.ThenBy(row => row.Id)
			.Select(row => new { row.Id, row.Topic, row.Payload, row.Version, row.Attempts })
			.FirstOrDefaultAsync(cancellationToken);
		if (candidate is null)
		{
			return false;
		}

		var lease = Guid.NewGuid();
		var until = now + options.Value.LeaseDuration;
		var acquired = await context.Set<OutboxRow>().Where(row => row.Id == candidate.Id && row.Version == candidate.Version &&
			(row.LeaseUntil == null || row.LeaseUntil <= now)).ExecuteUpdateAsync(update => update
				.SetProperty(row => row.LeaseId, lease).SetProperty(row => row.LeaseUntil, until)
				.SetProperty(row => row.Attempts, row => row.Attempts + 1)
				.SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
		if (acquired == 0)
		{
			return true;
		}

		try
		{
			await publisher.PublishAsync(candidate.Topic, codec.Decode(candidate.Payload), cancellationToken);
			await context.Set<OutboxRow>().Where(row => row.Id == candidate.Id && row.LeaseId == lease)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.PublishedAt, clock.GetUtcNow())
					.SetProperty(row => row.LeaseUntil, (DateTimeOffset?)null)
					.SetProperty(row => row.LeaseId, (Guid?)null).SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			logger.LogWarning("Publication failed for outbox record {MessageId} ({FailureType})", candidate.Id, exception.GetType().Name);
			var exhausted = candidate.Attempts + 1 >= options.Value.MaximumAttempts;
			await context.Set<OutboxRow>().Where(row => row.Id == candidate.Id && row.LeaseId == lease)
				.ExecuteUpdateAsync(update => update
					.SetProperty(row => row.NextAttemptAt, clock.GetUtcNow() + options.Value.RetryDelay)
					.SetProperty(row => row.QuarantinedAt, exhausted ? clock.GetUtcNow() : (DateTimeOffset?)null)
					.SetProperty(row => row.FailureCode, "messaging.publication_failed")
					.SetProperty(row => row.LeaseUntil, (DateTimeOffset?)null)
					.SetProperty(row => row.LeaseId, (Guid?)null)
					.SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
		}

		return true;
	}
}
