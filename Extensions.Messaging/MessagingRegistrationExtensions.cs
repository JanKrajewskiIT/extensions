using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Extensions.Messaging;

public static class MessagingRegistrationExtensions
{
	public static IServiceCollection AddKafkaMessaging<TMessage, TCodec, TPolicy>(this IServiceCollection services,
		IConfigurationSection kafka, IConfigurationSection outbox, Action<KafkaOptions>? configure = null)
		where TMessage : notnull
		where TCodec : class, IMessageCodec<TMessage>
		where TPolicy : class, IMessageDeliveryPolicy<TMessage>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(kafka);
		ArgumentNullException.ThrowIfNull(outbox);
		var options = services.AddOptions<KafkaOptions>().Bind(kafka);
		if (configure is not null)
		{
			options.PostConfigure(configure);
		}

		options.Validate(value => value.IsValid(), "Kafka requires valid bootstrap, security and bounded delivery settings.").ValidateOnStart();
		services.AddOptions<KafkaTopologyOptions>().Bind(kafka.GetSection("Topology"))
			.Validate(value => value.IsValid(), "Kafka topology requires explicit partitions, replication, ISR and bounded retention.")
			.ValidateOnStart();
		services.AddOptions<OutboxOptions>().Bind(outbox)
			.Validate<IOptions<KafkaOptions>>((value, connection) => value.MaximumAttempts > 0 &&
				value.PollInterval > TimeSpan.Zero && value.RetryDelay > TimeSpan.Zero &&
				value.LeaseDuration > TimeSpan.FromMilliseconds(connection.Value.DeliveryTimeoutMilliseconds) + TimeSpan.FromSeconds(10),
				"Outbox lease must exceed the Kafka delivery budget.").ValidateOnStart();
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<KafkaTopicCatalog>();
		services.TryAddSingleton<IMessageCodec<TMessage>, TCodec>();
		services.TryAddSingleton<IMessageDeliveryPolicy<TMessage>, TPolicy>();
		services.TryAddSingleton<IIntegrationPublisher<TMessage>, KafkaPublisher<TMessage>>();
		return services;
	}
}
