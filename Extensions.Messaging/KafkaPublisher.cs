using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public sealed class KafkaPublisher<TMessage> : IIntegrationPublisher<TMessage>, IDisposable where TMessage : notnull
{
	private readonly IProducer<string, string> producer;
	private readonly IMessageCodec<TMessage> codec;

	public KafkaPublisher(IOptions<KafkaOptions> options, IMessageCodec<TMessage> codec)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(codec);
		this.codec = codec;
		var settings = options.Value;
		producer = new ProducerBuilder<string, string>(new ProducerConfig
		{
			BootstrapServers = settings.BootstrapServers,
			SecurityProtocol = settings.SecurityProtocol,
			SaslMechanism = settings.SaslMechanism,
			SaslUsername = settings.Username,
			SaslPassword = settings.Password,
			SslCaLocation = settings.SslCaLocation,
			EnableIdempotence = true,
			Acks = Acks.All,
			MaxInFlight = 1,
			MessageSendMaxRetries = 3,
			MessageTimeoutMs = settings.DeliveryTimeoutMilliseconds,
			MessageMaxBytes = settings.MaximumMessageBytes,
			AllowAutoCreateTopics = false,
		}).Build();
	}

	public async ValueTask PublishAsync(string topic, TMessage message, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(message);
		var descriptor = codec.Describe(message);
		var result = await producer.ProduceAsync(topic, new Message<string, string>
		{
			Key = descriptor.PartitionKey,
			Headers = MessageTrace.Headers(descriptor.TraceParent),
			Value = codec.Encode(message),
		}, cancellationToken);
		if (result.Status != PersistenceStatus.Persisted)
		{
			throw new InvalidOperationException("Kafka did not acknowledge durable acceptance.");
		}
	}

	public void Dispose() => producer.Dispose();
}
