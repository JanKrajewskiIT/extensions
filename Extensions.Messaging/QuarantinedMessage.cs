namespace Extensions.Messaging;

public record QuarantinedMessage<TMessage, TArtifact>(Guid Id, string Consumer, SourcePosition Source,
	TMessage Message, string ReasonCode, DateTimeOffset QuarantinedAt, bool BlocksDependentScope, TArtifact Diagnostics);
