using Confluent.Kafka;

namespace Extensions.Messaging;

public sealed class KafkaOptions
{
	public string BootstrapServers { get; set; } = "";
	public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslSsl;
	public SaslMechanism? SaslMechanism { get; set; }
	public string? Username { get; set; }
	public string? Password { get; set; }
	public string? SslCaLocation { get; set; }
	public int DeliveryTimeoutMilliseconds { get; set; } = 30000;
	public int MaximumMessageBytes { get; set; } = 1048576;
	public bool IsValid() => !string.IsNullOrWhiteSpace(BootstrapServers) && Enum.IsDefined(SecurityProtocol) &&
		DeliveryTimeoutMilliseconds is > 0 and <= 600000 && MaximumMessageBytes is > 0 and <= 100000000 &&
		(SaslMechanism is null || Enum.IsDefined(SaslMechanism.Value)) &&
		(SecurityProtocol is not (SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl) ||
			SaslMechanism is not null && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password));
}
