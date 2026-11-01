using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class KafkaConsumerService<TMessage>(IServiceScopeFactory scopes,
	IMessageCodec<TMessage> codec, IMessageDeliveryPolicy<TMessage> policy,
	IOptions<KafkaOptions> kafkaOptions, IOptions<KafkaSubscriptionOptions> subscriptionOptions,
	TimeProvider clock, ILogger<KafkaConsumerService<TMessage>> logger) : BackgroundService where TMessage : notnull
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var settings = kafkaOptions.Value;
		var subscription = subscriptionOptions.Value;
		if (!subscription.HasValidProcessingPolicy() || !subscription.HasValidGroup(string.Empty))
		{
			throw new InvalidOperationException("Kafka subscriptions require bounded processing attempts, timeouts and retry delays.");
		}

		var blocked = new Dictionary<TopicPartition, long>();
		var assignmentVersion = 0L;
		TopicPartition? activePartition = null;
		var delivery = new KafkaDeliveryProcessor<TMessage>(scopes, settings.MaximumMessageBytes, codec, policy, subscription, clock, logger);
		void ForgetAssignments(IEnumerable<TopicPartitionOffset> partitions)
		{
			foreach (var partition in partitions)
			{
				if (activePartition is not null && partition.TopicPartition.Equals(activePartition))
				{
					assignmentVersion = checked(assignmentVersion + 1);
				}

				blocked.Remove(partition.TopicPartition);
			}
		}

		var builder = new ConsumerBuilder<string, string>(new ConsumerConfig
		{
			BootstrapServers = settings.BootstrapServers,
			SecurityProtocol = settings.SecurityProtocol,
			SaslMechanism = settings.SaslMechanism,
			SaslUsername = settings.Username,
			SaslPassword = settings.Password,
			SslCaLocation = settings.SslCaLocation,
			GroupId = subscription.Group,
			EnableAutoCommit = false,
			EnableAutoOffsetStore = false,
			AllowAutoCreateTopics = false,
			AutoOffsetReset = AutoOffsetReset.Error,
			MaxPollIntervalMs = checked((int)(subscription.ProcessingBudget.TotalMilliseconds + 60000)),
			FetchMaxBytes = settings.MaximumMessageBytes,
			IsolationLevel = IsolationLevel.ReadCommitted,
		}).SetPartitionsRevokedHandler((_, partitions) => ForgetAssignments(partitions))
			.SetPartitionsLostHandler((_, partitions) => ForgetAssignments(partitions));
		using var consumer = builder.Build();
		consumer.Subscribe(subscription.Topic);
		Exception? primaryFailure = null;
		var nextTelemetry = DateTimeOffset.MinValue;
		var telemetryCursor = 0;
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				await ResumeResolvedPartitionsAsync(consumer, blocked, subscription, stoppingToken);

				KafkaRetryBackoff.ResumeUnblocked(consumer, blocked);
				var record = consumer.Consume(KafkaRetryBackoff.PollInterval);
				if (clock.GetUtcNow() >= nextTelemetry)
				{
					telemetryCursor = await ObserveAsync(consumer, record, subscription, telemetryCursor, stoppingToken);
					nextTelemetry = clock.GetUtcNow() + TimeSpan.FromSeconds(5);
				}

				if (record is null)
				{
					await Task.Yield();
					continue;
				}

				activePartition = record.TopicPartition;
				await using (var scope = scopes.CreateAsyncScope())
				{
					var quarantine = scope.ServiceProvider.GetRequiredService<IMessageQuarantine>();
					var positions = new[] { new MessagePartition(record.Topic, record.Partition.Value) };
					if ((await quarantine.ReadBlockedAsync(subscription.Group, positions, stoppingToken)).Count > 0)
					{
						blocked[record.TopicPartition] = record.Offset.Value;
						consumer.Pause([record.TopicPartition]);
						continue;
					}
				}

				var recordAssignment = assignmentVersion;
				bool OwnsRecord() => recordAssignment == assignmentVersion && consumer.Assignment.Contains(record.TopicPartition);
				var outcome = await delivery.ProcessAsync(record, consumer, blocked, OwnsRecord, stoppingToken);
				if (outcome == KafkaDeliveryOutcome.OwnershipLost || !OwnsRecord())
				{
					logger.LogInformation("Kafka delivery released after rebalance at {Topic}/{Partition}/{Offset}",
						record.Topic, record.Partition.Value, record.Offset.Value);
					continue;
				}

				if (outcome is not (KafkaDeliveryOutcome.Completed or KafkaDeliveryOutcome.Filtered))
				{
					await QuarantineAsync(record, KafkaDeliveryProcessor<TMessage>.Reason(outcome), stoppingToken);
					logger.LogWarning("Kafka delivery ended as {Outcome} at {Topic}/{Partition}/{Offset}",
						outcome, record.Topic, record.Partition.Value, record.Offset.Value);
					if (subscription.RequiresContinuity)
					{
						blocked[record.TopicPartition] = record.Offset.Value;
						consumer.Pause([record.TopicPartition]);
						continue;
					}
				}

				stoppingToken.ThrowIfCancellationRequested();
				consumer.Commit(record);
				if (outcome is KafkaDeliveryOutcome.Completed or KafkaDeliveryOutcome.Filtered)
				{
					logger.LogDebug("Kafka delivery ended as {Outcome} at {Topic}/{Partition}/{Offset}",
						outcome, record.Topic, record.Partition.Value, record.Offset.Value);
				}
			}
		}
		catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
		{
			primaryFailure = exception;
			logger.LogDebug("Kafka consumer stopped for group {Group}", subscription.Group);
		}
		catch (Exception exception)
		{
			primaryFailure = exception;
			throw;
		}
		finally
		{
			try
			{
				consumer.Close();
			}
			catch (KafkaException exception) when (primaryFailure is not null)
			{
				logger.LogWarning("Kafka consumer cleanup failed with {ErrorCode}; the original failure is retained", exception.Error.Code);
			}
		}
	}

	private async ValueTask ResumeResolvedPartitionsAsync(
		IConsumer<string, string> consumer,
		Dictionary<TopicPartition, long> blocked,
		KafkaSubscriptionOptions subscription,
		CancellationToken token)
	{
		if (blocked.Count == 0)
		{
			return;
		}

		await using var scope = scopes.CreateAsyncScope();
		var quarantine = scope.ServiceProvider.GetRequiredService<IMessageQuarantine>();
		var positions = blocked.Select(value => new MessagePartition(value.Key.Topic, value.Key.Partition.Value)).ToArray();
		var unresolved = await quarantine.ReadBlockedAsync(subscription.Group, positions, token);
		var assigned = consumer.Assignment.ToHashSet();
		var unresolvedPartitions = unresolved.Select(row => new TopicPartition(row.Topic, new Partition(row.Partition))).ToHashSet();
		foreach (var partition in blocked.Keys.ToArray())
		{
			if (!unresolvedPartitions.Contains(partition) && assigned.Contains(partition))
			{
				consumer.Seek(new TopicPartitionOffset(partition, new Offset(blocked[partition])));
				blocked.Remove(partition);
			}
		}
	}

	private async ValueTask<int> ObserveAsync(IConsumer<string, string> consumer, ConsumeResult<string, string>? record,
		KafkaSubscriptionOptions subscription, int cursor, CancellationToken token)
	{
		try
		{
			var assigned = consumer.Assignment.OrderBy(value => value.Topic, StringComparer.Ordinal).ThenBy(value => value.Partition.Value).ToArray();
			if (assigned.Length == 0)
			{
				return 0;
			}

			var selected = assigned.Skip(cursor).Take(16).ToArray();
			var committed = consumer.Committed(selected, TimeSpan.FromSeconds(2));
			await using var operation = scopes.CreateAsyncScope();
			var telemetry = operation.ServiceProvider.GetService<IMessageConsumerTelemetry>();
			if (telemetry is null)
			{
				return 0;
			}

			foreach (var offset in committed)
			{
				token.ThrowIfCancellationRequested();
				if (offset.Offset.Value < 0)
				{
					continue;
				}

				var watermarks = consumer.QueryWatermarkOffsets(offset.TopicPartition, TimeSpan.FromSeconds(2));
				var messageAt = record is not null && record.TopicPartition.Equals(offset.TopicPartition) && record.Offset.Equals(offset.Offset) &&
					record.Message.Timestamp.Type != TimestampType.NotAvailable ? new DateTimeOffset(record.Message.Timestamp.UtcDateTime, TimeSpan.Zero) : (DateTimeOffset?)null;
				await telemetry.ObserveAsync(new ConsumerPartitionObservation(subscription.Name, offset.Topic, subscription.Group, offset.Partition.Value,
					clock.GetUtcNow(), offset.Offset.Value, watermarks.Low.Value, watermarks.High.Value, messageAt), subscription, token);
			}

			return cursor + selected.Length < assigned.Length ? cursor + selected.Length : 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception failure)
		{
			logger.LogWarning("Consumer SLI observation unavailable ({FailureType})", failure.GetType().Name);
			return 0;
		}
	}

	private async Task QuarantineAsync(ConsumeResult<string, string> record, string reason, CancellationToken cancellationToken)
	{
		var settings = subscriptionOptions.Value;
		await using var scope = scopes.CreateAsyncScope();
		var quarantine = scope.ServiceProvider.GetRequiredService<IMessageQuarantine>();
		await quarantine.StoreAsync(new QuarantineRequest(settings.Name, settings.Group,
			new SourcePosition(record.Topic, record.Partition.Value, record.Offset.Value),
			record.Message.Value ?? "null", reason, settings.RequiresContinuity), settings, cancellationToken);
	}
}
