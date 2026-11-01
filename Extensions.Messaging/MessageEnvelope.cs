using Extensions.Domain.Primitives;

namespace Extensions.Messaging;

public record MessageEnvelope<TPayload, TScope, TOrigin, TTrace> where TPayload : notnull where TScope : notnull where TTrace : notnull
{
	public MessageEnvelope(Guid messageId, string messageType, int schemaVersion, MessageKind kind,
		TScope scope, TOrigin origin, TTrace trace, DateTimeOffset occurredAt, DateTimeOffset publishedAt,
		DateTimeOffset? deadline, string producer, string producerVersion, string partitionKey, TPayload payload)
	{
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentNullException.ThrowIfNull(trace);
		ArgumentNullException.ThrowIfNull(payload);
		MessageId = DomainGuard.Identity(messageId, nameof(messageId));
		MessageType = DomainGuard.Required(messageType, nameof(messageType));
		SchemaVersion = DomainGuard.Positive(schemaVersion, nameof(schemaVersion));
		Kind = DomainGuard.Defined(kind, nameof(kind));
		Scope = scope;
		Origin = origin;
		Trace = trace;
		OccurredAt = occurredAt.ToUniversalTime();
		PublishedAt = publishedAt.ToUniversalTime();
		Deadline = deadline?.ToUniversalTime();
		Producer = DomainGuard.Required(producer, nameof(producer));
		ProducerVersion = DomainGuard.Required(producerVersion, nameof(producerVersion));
		PartitionKey = DomainGuard.Required(partitionKey, nameof(partitionKey));
		if (kind == MessageKind.Command && deadline is null || deadline <= publishedAt)
		{
			throw new ArgumentException("A command requires a deadline after publication.", nameof(deadline));
		}

		Payload = payload;
	}

	public Guid MessageId { get; }
	public string MessageType { get; }
	public int SchemaVersion { get; }
	public MessageKind Kind { get; }
	public TScope Scope { get; }
	public TOrigin Origin { get; }
	public TTrace Trace { get; }
	public DateTimeOffset OccurredAt { get; }
	public DateTimeOffset PublishedAt { get; }
	public DateTimeOffset? Deadline { get; }
	public string Producer { get; }
	public string ProducerVersion { get; }
	public string PartitionKey { get; }
	public TPayload Payload { get; }
}
