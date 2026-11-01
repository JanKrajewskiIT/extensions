# Extensions.Messaging

Opt-in Kafka publisher/consumer, ordered leased outbox relay, transactional inbox/outbox staging and EF mappings. Hosts own message codecs, scope/origin validation, admission budgets, topic names, quarantine artifacts and data permissions.

## Compose a host

- Call `AddKafkaMessaging<TMessage,TCodec,TPolicy>` with Kafka and Outbox sections; no worker is registered automatically.
- Register an owner DbContext and call `AddOutboxInbox()` in its model; call `AddMessageQuarantine()` in the quarantine owner's model.
- Register `IMessageProcessor<TMessage>` handlers and `IMessageQuarantine`; optionally register `IMessageConsumerTelemetry`.
- Register `OutboxRelay<TContext,TMessage>`, configured `KafkaConsumerService<TMessage>`, health check and topology service explicitly.
- Supply topic names through the bounded catalog; startup provisioning remains explicit and opt-in.

Stage inbox receipts and outbox messages inside the same owner transaction as durable effects; staging does not save or commit. Consumer identity is (Consumer, ScopeKey, MessageId), with a conflicting-hash outcome. Publication remains at least once; broker acknowledgement cannot eliminate the crash window before marking an outbox row published.

The delivery policy returns only Malformed, Rejected or BudgetExhausted for terminal classification; null means retry within the bounded delivery budget. Shutdown is propagated by the engine. Attempt scopes and timeout fences remain active until awaited processing ends. The policy supplies a disposable work-budget scope. Subscriber options are captured for the consumer lifetime and passed unchanged to host callbacks.

The codec owns deserialization, validation and canonical encoding. Its descriptor supplies the partition key and optional valid W3C traceparent. Product scope/origin/hash types can use the generic message records without introducing dependencies from this package back to the product.

SaslSsl is the default; explicit security settings, credentials, CA locations and all deployment values come from the host. Options validation emits generic errors rather than values. Infrastructure does not reference Messaging, so existing Infrastructure consumers do not acquire Confluent.Kafka. Messaging itself carries that SDK's managed/native runtime dependencies.

Source is unreleased. No build, broker or package-consumer verification is implied. Historical migrations remain with context owners; compare their relational model after CLR type moves before rollout.
