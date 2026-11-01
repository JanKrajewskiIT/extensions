namespace Extensions.Messaging;

public sealed record QuarantineRequest(string SubscriptionName, string Consumer, SourcePosition Source,
	string Payload, string ReasonCode, bool BlocksScope);
