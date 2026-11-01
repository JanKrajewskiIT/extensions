using Extensions.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Extensions.Messaging;

public static class MessageTransactions
{
	public static async ValueTask<MessageAcceptance> AcceptAsync(DbContext context, string consumer, string scopeKey,
		Guid messageId, string payloadHash, DateTimeOffset processedAt, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
		ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
		if (messageId == Guid.Empty)
		{
			throw new ArgumentException("A message requires an identity.", nameof(messageId));
		}

		await context.LockAsync(DatabaseLockKeys.Create("inbox", consumer, scopeKey, messageId.ToString("N")), cancellationToken);
		var inbox = context.Set<InboxRow>();
		var existing = inbox.Local.SingleOrDefault(row => row.Consumer == consumer && row.ScopeKey == scopeKey && row.MessageId == messageId)
			?? await inbox.AsNoTracking().SingleOrDefaultAsync(row => row.Consumer == consumer && row.ScopeKey == scopeKey &&
				row.MessageId == messageId, cancellationToken);
		if (existing is not null)
		{
			return existing.PayloadHash == payloadHash ? MessageAcceptance.Duplicate : MessageAcceptance.ConflictingDuplicate;
		}

		inbox.Add(new InboxRow
		{
			Consumer = consumer,
			ScopeKey = scopeKey,
			MessageId = messageId,
			PayloadHash = payloadHash,
			ProcessedAt = processedAt.ToUniversalTime(),
		});
		return MessageAcceptance.Accepted;
	}

	public static async ValueTask StageAsync(DbContext context, string topic, Guid messageId, string partitionKey,
		string payload, DateTimeOffset now, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(payload);
		if (messageId == Guid.Empty)
		{
			throw new ArgumentException("A message requires an identity.", nameof(messageId));
		}

		await context.LockAsync(DatabaseLockKeys.Create("outbox", partitionKey), cancellationToken);
		var streams = context.Set<OutboxStreamRow>();
		var stream = streams.Local.SingleOrDefault(row => row.StreamKey == partitionKey)
			?? await streams.SingleOrDefaultAsync(row => row.StreamKey == partitionKey, cancellationToken);
		if (stream is null)
		{
			stream = new OutboxStreamRow { StreamKey = partitionKey };
			streams.Add(stream);
		}

		stream.Sequence = checked(stream.Sequence + 1);
		context.Set<OutboxRow>().Add(new OutboxRow
		{
			Id = messageId,
			Topic = topic,
			StreamKey = partitionKey,
			StreamSequence = stream.Sequence,
			Payload = payload,
			CreatedAt = now.ToUniversalTime(),
			NextAttemptAt = now.ToUniversalTime(),
		});
	}
}
