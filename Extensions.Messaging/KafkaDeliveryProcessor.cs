using System.Text;
using Confluent.Kafka;
using Extensions.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extensions.Messaging;

internal sealed class KafkaDeliveryProcessor<TMessage>(IServiceScopeFactory scopes, int maximumMessageBytes,
	IMessageCodec<TMessage> codec, IMessageDeliveryPolicy<TMessage> policy,
	KafkaSubscriptionOptions settings, TimeProvider clock, ILogger logger) where TMessage : notnull
{
	public static string Reason(KafkaDeliveryOutcome outcome) => outcome switch
	{
		KafkaDeliveryOutcome.Malformed => "messaging.malformed",
		KafkaDeliveryOutcome.Rejected => "messaging.processing_rejected",
		KafkaDeliveryOutcome.AttemptsExhausted => "messaging.processing_exhausted",
		KafkaDeliveryOutcome.BudgetExhausted => "messaging.processing_budget_exhausted",
		_ => throw new ArgumentOutOfRangeException(nameof(outcome)),
	};

	public async ValueTask<KafkaDeliveryOutcome> ProcessAsync(ConsumeResult<string, string> record, IConsumer<string, string> consumer,
		IReadOnlyDictionary<TopicPartition, long> blocked, Func<bool> ownsRecord, CancellationToken stoppingToken)
	{
		var started = clock.GetTimestamp();
		var processingBudget = settings.ProcessingBudget;
		using var budget = new CancellationTokenSource(processingBudget, clock);
		using var operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, budget.Token);
		using var executionBudget = policy.BeginBudget(settings, operation.Token);
		using var activity = MessageTrace.Start(record.Message.Headers);
		TMessage message;
		MessageDescriptor descriptor;
		try
		{
			if (record.Message.Value is null || Encoding.UTF8.GetByteCount(record.Message.Value) > maximumMessageBytes)
			{
				return KafkaDeliveryOutcome.Malformed;
			}

			message = codec.Decode(record.Message.Value);
			descriptor = codec.Describe(message);
			if (descriptor.PartitionKey != record.Message.Key)
			{
				return KafkaDeliveryOutcome.Malformed;
			}
		}
		catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
		{
			return KafkaDeliveryOutcome.Malformed;
		}

		if (!policy.Accepts(message, settings) || settings.MessageTypes.Length > 0 &&
			!settings.MessageTypes.Contains(descriptor.MessageType, StringComparer.Ordinal))
		{
			return KafkaDeliveryOutcome.Filtered;
		}

		for (var attempt = 1; ; attempt++)
		{
			stoppingToken.ThrowIfCancellationRequested();
			if (!ownsRecord())
			{
				return KafkaDeliveryOutcome.OwnershipLost;
			}

			var remaining = processingBudget - clock.GetElapsedTime(started);
			if (remaining <= TimeSpan.Zero || budget.IsCancellationRequested)
			{
				return KafkaDeliveryOutcome.BudgetExhausted;
			}

			try
			{
				logger.LogDebug("Kafka processing attempt {Attempt} started at {Topic}/{Partition}/{Offset}",
					attempt, record.Topic, record.Partition.Value, record.Offset.Value);
				using var timeout = new CancellationTokenSource(remaining < settings.ProcessingTimeout ? remaining : settings.ProcessingTimeout, clock);
				using var processing = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, timeout.Token);
				using var fence = new OperationWriteScope(new CancellationWriteFence("message-processing", processing.Token));
				processing.Token.ThrowIfCancellationRequested();
				await executionBudget.TakeAttemptAsync(processing.Token);
				await using var scope = scopes.CreateAsyncScope();
				var processor = scope.ServiceProvider.GetServices<IMessageProcessor<TMessage>>()
					.SingleOrDefault(handler => handler.MessageType == descriptor.MessageType)
					?? throw new InvalidDataException("Unsupported message type.");
				await processor.ProcessAsync(message, processing.Token);
				processing.Token.ThrowIfCancellationRequested();
				return KafkaDeliveryOutcome.Completed;
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception)
			{
				logger.LogWarning("Kafka processing attempt {Attempt} failed with {FailureType} at {Topic}/{Partition}/{Offset}",
					attempt, exception.GetType().Name, record.Topic, record.Partition.Value, record.Offset.Value);
				if (policy.Classify(exception) is { } terminal)
				{
					return terminal;
				}
			}

			remaining = processingBudget - clock.GetElapsedTime(started);
			if (remaining <= TimeSpan.Zero || budget.IsCancellationRequested)
			{
				return KafkaDeliveryOutcome.BudgetExhausted;
			}

			if (attempt >= settings.MaximumAttempts)
			{
				return KafkaDeliveryOutcome.AttemptsExhausted;
			}

			var delay = KafkaRetryBackoff.Delay(settings, attempt);
			if (delay >= remaining)
			{
				return KafkaDeliveryOutcome.BudgetExhausted;
			}

			try
			{
				if (!KafkaRetryBackoff.Wait(consumer, blocked, ownsRecord, delay, clock, logger, operation.Token))
				{
					return KafkaDeliveryOutcome.OwnershipLost;
				}
			}
			catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && budget.IsCancellationRequested)
			{
				return KafkaDeliveryOutcome.BudgetExhausted;
			}
		}
	}
}
