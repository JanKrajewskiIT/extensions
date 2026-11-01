using System.Text.Json.Serialization;

namespace Extensions.Messaging;

public record EncodedMessage<TEnvelope, THash, TContract> where TEnvelope : notnull where THash : notnull
{
	public EncodedMessage(TEnvelope envelope, THash payloadHash, TContract? payloadContract = default)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		ArgumentNullException.ThrowIfNull(payloadHash);
		Envelope = envelope;
		PayloadHash = payloadHash;
		PayloadContract = payloadContract;
	}

	public TEnvelope Envelope { get; }
	public THash PayloadHash { get; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public TContract? PayloadContract { get; }
}
