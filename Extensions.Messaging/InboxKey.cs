namespace Extensions.Messaging;

public record InboxKey<TScope>(string Consumer, TScope Scope, Guid MessageId);
