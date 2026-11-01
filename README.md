# Extensions

Shared .NET libraries for Recron and QuantCore.

Documentation: [Recron project docs](https://docs.bluebraces.online/projects/extensions/).

## Host configuration

`ConfigurationExtensions.AddHostDefaults<TBuilder>()` registers OpenTelemetry, OTLP export when configured, self health checks, service discovery and standard HTTP resilience for an `IHostApplicationBuilder`. It does not register authentication, OpenAPI, persistence, caching, application handlers or jobs. ASP.NET Core hosts map the existing probes through `app.UseDefaultHealthChecks()` from `Extensions.Infrastructure.Metrics`.

QuantCore uses this smaller bootstrap and supplies a retry policy that disables retries for unsafe HTTP methods:

```csharp
using Extensions.Infrastructure;
using Extensions.Infrastructure.Metrics;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);
builder.AddHostDefaults(options => options.Retry.DisableForUnsafeHttpMethods());

var app = builder.Build();
app.UseDefaultHealthChecks();
await app.RunAsync();
```

Recron continues to use `RunModuleAsync` / `AddServiceDefaults` with required Keycloak configuration, structured logging, API middleware, validation, error handling and caching. `AddServiceDefaults` delegates the common registrations to `AddHostDefaults`; its existing standard HTTP retry policy and authentication requirements are preserved. Call one bootstrap per host, not both. `/health` and `/alive` only reflect registered checks; the default self-check does not prove business readiness.

`Extensions.Application` provides command/query handler contracts and pagination. `Extensions.Domain` provides entity/audit bases, current-user contracts, roles and a domain exception. They do not yet provide a financial ledger, event envelope, outbox/inbox, broker integration or simulation clock. These are not prerequisites for an empty solution skeleton. Use platform `TimeProvider` or module-owned contracts when implementing the actual temporal requirements rather than adding an empty shared library.

The pending 1.9.0 release includes the new `AddHostDefaults` API and the `AiRequestExecutor` overload accepting caller-owned `ChatOptions`; neither is present in the cached 1.8.1 package used by Recron. `Directory.Build.props` owns the shared package version. QuantCore currently uses an explicit local source bridge for the unreleased Infrastructure/Messaging/Jobs APIs. Recron keeps its package selection. Release requires separate pack/consumer verification of every package listed in Extensions.slnx; no package was published or existing version replaced. Recron's package selection is unchanged.

## Neutral contracts and coded failures

The unreleased source now contains `Extensions.Domain.Primitives`: structural guards, UTC half-open intervals, SHA-256 content identity, immutable version references and correlation/causation. `Extensions.Domain.Serialization.PayloadSerializer` retains the existing canonical representation: ordinal object keys, original array order, decimal numbers, string enums, additive-field tolerance and a depth limit of 64. Its options are read-only. `Deserialize<T>` requires a non-null document; `Hash` hashes canonical UTF-8 JSON. Constructor/property shapes and identity algorithms retain their original wire meaning.

`Extensions.Application.Documents.JournalDocument` and the `Pagination.JournalPage<T>` / cursor contracts preserve immutable version identity and cursor/filter binding. Existing `PagedQuery` / `PagedResponse` offset pagination remains available with its original behavior. Application now references Domain for these neutral identities; no external package was added.

Failures may implement `ICodedException`, exposing `Code` and `FailureKind`. The concrete taxonomy is `RequestValidationException`, `ResourceNotFoundException`, `ConcurrencyConflictException`, `BusinessRuleException`, `DependencyFailureException` and the existing `DataInconsistencyException`. Dependency failures have explicit retryability and optional delay; a delay is not cancellation or permission to repeat an unsafe operation. All wrappers preserve their cause. The original inconsistency constructors preserve their diagnostic message and use `data.inconsistent`; an additive three-argument constructor supplies a machine code.

Codes are application-owned contracts. Hosts choose HTTP/error mappings and must not expose diagnostic messages or dependency bodies. No existing handler/bootstrap mapping is changed by adding these types. These source additions require a new package release before a consumer selects them; existing published versions have not been replaced.

## Transactional command pipeline

`AddHandlersFromAssembly<TContext>` is an explicit PostgreSQL-backed alternative to the existing nongeneric registration. Both `ICommandHandler<TCommand>` and `ICommandHandler<TCommand,TResponse>` keep `ExecuteAsync(command, ct)`. Queries and the nongeneric registration retain their existing behavior; Recron does not opt into the new pipeline automatically.

The generic registration composes validation, transaction/retry, idempotency and local write audit. Commands/results must be JSON contracts supported by the canonical codec. The request is captured once and deserialized separately for each attempt, so one failed attempt cannot mutate the input of the next one. Default canonical request and response limits are each 1 MiB; the registration options can set service-specific limits.

Register an explicit `ICommandContextProvider<TCommand>` for each command. It authorizes the invocation and supplies CommandId, data owner, actor, partition, versioned operation and optional trace. These values are not inferred from arbitrary object properties or accepted as authority merely because a request contains them. The provider runs again in every attempt scope and must return the same context; changed authority refuses execution. Scoped background delegation can be rebound through `ICommandAttemptInitializer`; the initializer only binds trusted scoped state and does not replace independent authorization by the context provider or handler.

Assume an existing `ApplicationDbContext` with a service-owned `Documents` set whose entities have `Id`, `Owner` and `Title`, and the application's authenticated current-user registration:

```csharp
using Extensions.Application.Abstractions;
using Extensions.Application.Commands;
using Extensions.Domain.Abstractions;
using Extensions.Domain.Exceptions;
using Extensions.Infrastructure;
using Extensions.Infrastructure.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("application"), postgres => postgres.EnableRetryOnFailure()));
builder.Services.AddScoped<ICommandContextProvider<RenameDocument>, RenameDocumentContext>();
builder.Services.AddHandlersFromAssembly<ApplicationDbContext>(typeof(RenameDocumentHandler).Assembly);

public sealed record RenameDocument(Guid CommandId, Guid DocumentId, string Title) : ICommand<Guid>;

public sealed class RenameDocumentContext(ICurrentUserService currentUser) : ICommandContextProvider<RenameDocument>
{
    public CommandContext Create(RenameDocument command)
    {
        var subject = currentUser.UserId?.ToString("D") ?? throw new UnauthorizedAccessException();
        return new(command.CommandId, subject, subject, "documents", "documents.rename.v1");
    }
}

public sealed class RenameDocumentValidator : AbstractValidator<RenameDocument>
{
    public RenameDocumentValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class RenameDocumentHandler(ApplicationDbContext context, ICurrentUserService currentUser)
    : ICommandHandler<RenameDocument, Guid>
{
    public async Task<Guid> ExecuteAsync(RenameDocument command, CancellationToken ct = default)
    {
        var subject = currentUser.UserId?.ToString("D") ?? throw new UnauthorizedAccessException();
        var document = await context.Documents.SingleOrDefaultAsync(
            row => row.Id == command.DocumentId && row.Owner == subject, ct)
            ?? throw new ResourceNotFoundException("document.unavailable");
        document.Title = command.Title;
        return document.Id;
    }
}
```

Append `modelBuilder.AddCommandPipeline()` to that context's existing model configuration, optionally specifying its service-owned schema. The opt-in maps `command_receipts` and `command_audit`; it never modifies a consumer model or migrates a database automatically. The consuming service owns its migration, permissions, retention and protection of retained results. Neither row derives from `AuditEntity`: the acting reviewer/workload and the data owner need not be the same principal, and a CreatedBy filter is not a substitute for owner authorization.

Each attempt receives a fresh DI scope, scoped context and concrete handler. `CreateExecutionStrategy` uses the host's configured provider retry policy; the caller cancellation token also bounds retry waits. The caller must have no ambient/local transaction or pending writes. Nested pipeline commands are refused; compose local operations within one command instead. All handler writes must use the selected TContext; the handler must not commit/replace that transaction, detach work or make external effects. Use a transactional outbox for external delivery. Business concurrency/version/fencing checks still belong to the owning handler.

The pipeline's local metadata audit does not replace more detailed application-owned critical-write evidence or access checks. Existing context save participants/interceptors continue to run. Cleanup failures are logged by stage/type without exception messages or payloads and cannot replace a primary handler/commit failure.

After acquiring a PostgreSQL transaction advisory lock, the ReadCommitted transaction reads the receipt using a fresh statement snapshot. The key binds owner, partition, versioned operation and CommandId. A matching request hash returns the original result without executing the handler or adding a second audit entry; a different hash is `command.identity_reused`. The original receipt must retain its matching local audit and result checksum. Keep the operation's version stable for a compatible result shape, and use a new operation version when the contract becomes incompatible. Replay supports intentionally null results and commands without a response.

Handler writes, metadata-only audit, result hash and receipt commit together. An oversized or unserializable result rolls back local writes. A transient failure, including a lost commit acknowledgement when the provider retries it, starts another fresh attempt which first checks the durable outcome. A caller timeout/cancellation is not proof of rollback: a later authorized retry must keep the same CommandId and canonical content. Audit contains actor/owner, operation, partition, request hash, trace and time; payloads are not logged or included in that audit. Receipt result JSON remains protected service data. Applied receipts and audit must not be edited to reuse an identity.

No build, pack, migration, test or consumer execution has been performed for these source additions. Validation and release of the next immutable package version remain separate operations.

## Versioned artifact storage (unreleased source)

`Extensions.Storage` provides exact-version S3 artifacts, SHA-256/length verification, bounded UTF-8/JSON codecs and explicit host dependency registration. It is an opt-in separate package: the existing Infrastructure package and Recron gain no S3 dependency. See [Storage usage and ownership](Extensions.Storage/README.md). Consumer adoption, immutable version selection, package inspection and publication remain separate release work.

## Opt-in HTTP pipeline (unreleased source)

Infrastructure now supplies correlation and cooperative request deadlines, explicit host error mapping, one coded problem JSON body, command header guards, mutation observation, static-console headers and a neutral JSON Schema exporter. See [HTTP pipeline usage](Extensions.Infrastructure/Http/README.md). The existing RunModuleAsync signature remains; enabling request middleware and mutation audit is explicit. The new safe exception JSON shape is a consumer-visible release change, including for Recron, and must be classified before publication. No package version is changed here.

## Recron CV ownership

CV input records and PortfolioTextBuilder belong to Recron's local `Recron.Cv` project, consumed by Portfolio.Api and Organizer.Api. They no longer form part of the Infrastructure source API. Generic AI mechanics stay here; the shared library does not reference Recron. The [release notes](CHANGELOG.md) describe the namespace/assembly migration and its compatibility impact. No package is added or published by this move.

## Database write boundary (unreleased source)

Infrastructure's existing BaseApplicationDbContext now coordinates opt-in write participants, transaction/session locks, nested fences, local critical audit staging, registry publication, configuration revisions/reviews/drift and bounded transactional observations. Owner-managed models keep explicit owner admission and historical mappings; the legacy current-user context constructor and mutable audit defaults are retained. [Database composition](Extensions.Infrastructure/Database/README.md) explains ordering and host responsibilities. The AuditInterceptor accepts an explicit trusted actor and TimeProvider; CreatedBy remains distinct from a financial owner.

## Kafka messaging (unreleased source)

`Extensions.Messaging` supplies opt-in Kafka transport, transactional inbox/outbox models and staging, ordered leased relay, topology, retry, tracing and quarantine ports. Hosts supply domain codecs, data permissions, work budgets, topics and artifact persistence; the package has no product database dependency. [Messaging composition](Extensions.Messaging/README.md) describes ownership. Infrastructure does not reference this package, so Recron does not gain Kafka through its existing bootstrap. Package version selection, clean-consumer validation and publication remain separate release work.

## Durable jobs and recurring work

The opt-in [Extensions.Jobs package](Extensions.Jobs/README.md) now contains the extracted durable-job state machine, PostgreSQL job/schedule persistence, hosted runner and recurring scheduler, handler discovery and execution-budget lifecycle. Hosts supply their own context, codecs, actor, policies, handlers and event factories. Jobs references Messaging; Infrastructure has no Jobs reference. Work-budget stores retain one authoritative partition rather than copying counters per worker. This is unreleased source; builds, model/JSON comparisons and package/consumer checks remain deferred.
