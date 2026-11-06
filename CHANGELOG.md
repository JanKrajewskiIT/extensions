# Release notes

## Unreleased — explicit manifest definitions

Infrastructure.Http adds opt-in `ManifestDefinition<TReference>` and `IManifestDefinitionProvider<TReference>` for explicit owner catalogs. The typed factory binds a document type, reference reader and semantic validator without assembly scanning, automatic registration or product dependencies. QuantCore supplies its six module providers; strict serialization, schema compilation, module authorization and publication receipts remain consumer-owned. No package version or dependency changed. Builds, schema/hash comparison, packing and clean-consumer acceptance remain unrun.

## Unreleased — payload schema metadata

Infrastructure.Http adds opt-in `PayloadContractSchemas` and `JsonPayloadSchema`. The bounded cache is keyed by the payload type and the exact read-only JSON options instance, exports through the existing ContractSchemas helper and hashes canonical JSON through Domain serialization. Owners supply their payload DTOs and retain topic, producer, version and envelope policy. QuantCore removes its former Persistence.MessageSchemas implementation and adopts this helper through the existing source bridge. No package dependency/version was added or changed; consumer schema/hash comparisons and package/build acceptance remain unrun.

## Unreleased — provider integrations

Adds Extensions.Integrations as an opt-in source package, depending only on Extensions.Domain. It contains bounded provider HTTP/JSON transport, a bounded TTL cache, local/distributed quota gates, the shared UTC-window calculation and renewable leases. Quota authority, product priorities, execution budgets and logging enter through explicit ports; no EF/Jobs/ASP.NET or product dependency is added.

QuantCore adopts the source package, removes the replaced Transport implementations and rebuilds its adapter references. Its provider policy/control DTOs move to MarketData.Contracts while retaining JSON fields and canonical policy hash rules. Transport/gate types move to Extensions.Integrations; source/binary consumers must update namespaces and rebuild. Generic transient failures now carry DependencyFailureException metadata instead of RetryableJobException. Provider raw capture remains on QuantCore's existing artifact facade pending Storage adoption.

No package version was changed or published. Builds, packing, restore, runtime and consumer checks remain unrun. Release must validate exact package contents and clean consumers; source-bridge compilation is not package-release evidence.


## Unreleased — CV ownership

### Breaking API change

The CV-specific `PortfolioInputData`, its seven nested input record types and `PortfolioTextBuilder` have moved from `Extensions.Infrastructure.Ai` to the `Recron.Cv` namespace and assembly in the Recron repository. This is a source and binary API change in Infrastructure; the next immutable package release must classify it accordingly. The package version has not been changed or published.

### Migration

Recron's Portfolio.Api and Organizer.Api reference their local, non-packable Recron.Cv project. CV-only source files import that namespace; files also using generic AI helpers use explicit CV type aliases so they work while an older Extensions package still exposes the former type names. Consumer binaries must be rebuilt; references to the old namespace/assembly must be migrated.

CV property names, constructor parameter order, nullability and nested collection shapes are retained. The renderer keeps its section order, labels, separators, blank lines, fallback values and contact-detail inclusion flag. ATS still requests contact details; CV matching and cover letters use the default mode. The negotiation input uses the same owned DTO. JSON and prompt execution checks remain deferred.

The generic AI request executor, policy, response helper and failure types remain in Infrastructure. Infrastructure has no reference to Recron.Cv. No new NuGet dependency, forwarding assembly, schema migration or service resource is introduced.

See [Recron CV ownership](README.md#recron-cv-ownership) for the source boundary. Builds, package inspection and consumer/runtime checks have not been run for this change; immutable package release and adoption remain separate work.

## Unreleased — Database write composition

BaseApplicationDbContext now owns the shared fenced save algorithm and accepts explicit owner participants. Its existing current-user constructor and default AuditInterceptor actor/clock behavior are retained; a new owner-managed constructor, query-owner override and trusted audit actor/TimeProvider path support delegated workloads without treating CreatedBy as financial ownership.

Transaction advisory locks, nonpooled session leases, fence scopes, critical metadata/staging, registry mapping/publication and configuration review/drift/read-set mechanics now live in Infrastructure.Database. QuantCore adopts them through its existing source bridge and keeps its domain policies, schema mappings, event identities and compatibility adapters. Its moved neutral ports/types require source consumers to use the shared namespace; package versions are unchanged.

The opt-in ResourceConcurrencyFilter retains 409/resource_conflict and now uses the common HTTP problem shape. Registry model metadata and canonical critical descriptor/audit serialization must be compared during final validation. Existing schemas and migrations are retained in source; no database, build, pack, test or publication command has been run.

## Unreleased — Messaging extraction

Added the separate Extensions.Messaging source package using the existing Confluent.Kafka version from QuantCore. Kafka delivery, options validation, topology, health checks, traceparent propagation, ordered leased outbox, inbox admission, quarantine models/ports and generic message records belong to this package. Infrastructure has no Messaging reference.

QuantCore adopts the source project, retains closed domain record adapters and supplies codec, scope/origin/schema admission, work budgets, quarantine artifacts and topic factories. Wire fields and relational mappings are retained in source; enum ownership and record inheritance change CLR contracts and require consumer rebuilds plus canonical JSON/schema/model comparisons. No SDK/package version, release or migration was executed. Builds, broker behavior, package packing and clean-consumer checks remain deferred.

## Unreleased — Jobs extraction

Added the separate Extensions.Jobs source package, using existing Messaging/Infrastructure dependencies with no new external package. It owns generic job state/lease/checkpoint/retry, PostgreSQL job and recurring models/stores, hosted runner/scheduler, typed scoped operations, IJobHandler<TJob> assembly discovery and work reservation/execution lifecycle. Owner codecs, event factories, approval/priority policy, durable budget store and handler business effects remain explicit.

QuantCore removes its Processing implementation project and adopts Jobs through the source bridge; domain adapters and unchanged historical migration sources live in QuantCore.Persistence. Generic models are composed into the owner's context, and the platform budget retains its single PostgreSQL partition. Infrastructure and Recron do not opt into Jobs implicitly. Moved enums/interfaces and closed record inheritance change CLR contracts; preserve canonical payloads, lock/occurrence identities and EF metadata during deferred validation. No version, migration, runtime execution or publication was performed.
