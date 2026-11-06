# Database composition

## Startup migration helper

`MigrateDatabase<TContext>(services, cancellationToken)` retains its existing name and signature and delegates to the additive `MigrateDatabaseAsync<TContext>` entry point. Both resolve the scoped context/logger and probe database reachability before reading pending migrations. The helper makes at most five `CanConnectAsync` calls, returning immediately on the first `true`. Each `false` schedules the next call after 2, 4, 6 or 8 seconds; there is no delay after the last attempt. The 20 seconds of backoff excludes provider connection time and any provider-level execution-strategy retries, so this is not a total startup deadline. Delays use the host's registered `TimeProvider`, falling back to `TimeProvider.System`.

Exhaustion logs only the stable code and attempt count, then throws `DatabaseStartupException` with `Code = database.startup_unavailable`, `Kind = Dependency` and `Attempts = 5`. Its summary has no endpoint, connection string or provider response. EF's boolean probe suppresses connection exceptions and does not expose their original cause; an all-false sequence therefore has no invented `InnerException`. Exceptions that escape the probe propagate unchanged. Cancellation is checked before and after each probe and passed through the delay, so even a provider cancellation converted to `false` remains caller cancellation instead of exhaustion.

Once reachable, pending migrations run once through the existing EF migration operation. This helper does not retry failed DDL, create a migration lock across hosts or alter command timeouts. If migrations were applied, Npgsql type mappings are reloaded on the context-owned connection. A connection opened by this reload is opened/closed through `Database.OpenConnectionAsync` / `CloseConnectionAsync`; an already-open connection stays open, and the helper never disposes the context's connection. If reload and close both fail, the original reload failure (including cancellation) propagates and only the cleanup exception type is logged. A close failure after a successful reload propagates normally.

These are unreleased source changes. The existing Recron callers retain the legacy entry point and require a new package selection to receive the fix; QuantCore has its own migrator and does not call this helper. No database, migration, startup, cancellation scenario or build has been executed to validate this change.

## Execution strategies and explicit transactions

The original `AddPostgresDbContext<TContext>(connectionName, configureDataSource, configureNpgsql)` overload retains its signature and defaults: up to three retries, a maximum retry delay of five seconds, the same provider error-code list, data-source setup and audit interceptor. Existing consumers need no registration changes.

Contexts used by helpers that own explicit transactions can opt out of the default retry strategy:

```csharp
builder.AddPostgresDbContext<ApplicationDbContext>(
    "application",
    PostgresRetryPolicy.NoRetries,
    configureNpgsql: options => options.MapEnum<ApplicationState>());
```

The additive overload requires a defined `PostgresRetryPolicy`; `TransientFailures` selects the original defaults and `NoRetries` skips `EnableRetryOnFailure`. The existing Npgsql callback still runs last and can override the resulting strategy. This option does not change database connection timeouts, migration behavior or application-level job retries.

`DatabaseTransactions.BeginAsync` checks the effective provider strategy's `RetriesOnFailure` before opening a transaction. If it can independently retry, the helper throws one `InvalidOperationException` explaining the configuration conflict and the supported alternatives, before that transaction or its write callbacks begin. `ReviewedConfigurationStore`, the revocation transaction in `ConfigurationDriftGuard`, `EfJobStore` and every recurring-schedule transaction use this entry point. `DatabaseWritePipeline` applies the same check before opening or joining its transactional save, and `DatabaseLocks.LockAsync` checks before issuing the advisory-lock SQL in a caller-owned transaction. Ordinary unfenced saves keep their existing path.

These helpers do not replay callbacks, tracked writes, clocks or external effects. A caller that needs retries must own the complete transaction unit through an EF execution strategy and restore its state for each attempt; an active outer strategy handles nested operations according to EF's effective strategy behavior. `EfCommandTransaction` already owns this pattern, including a fresh scope/context per attempt, and retains its existing execution-strategy path. Disabling provider retries does not make an uncertain commit safe to repeat; command receipts and reconciliation remain application responsibilities.

QuantCore's current context registration does not enable provider retries and consumes these changes through its Extensions source references. Existing Recron registrations continue to use the original overload; deployment through NuGet requires a new Extensions release. No build, database transaction or executable retry scenario has been run for this change.

## Shared persistence components

BaseApplicationDbContext is the shared save boundary. The existing options/current-user constructor keeps legacy AuditEntity CreatedBy filters and assembly configuration. The protected options-only constructor supports explicit owner-managed models; override ConfigureModel and CreateSavePlan, and enforce owner/resource authority in the service. Existing QuantCore module adapters now derive from this base; their registry mapping uses AddDocumentRegistry with explicit legacy column names.

A DatabaseSavePlan supplies admissions, observations and a local ICriticalWriteAudit factory. Admissions run before ordinally ordered ambient fences; observations follow; projected audit rows stage only after complete projection. SaveChangesAsync persists without accepting state, repeats fences, commits only an owned transaction, and then optionally accepts state. Failed saves detach only their new audit rows. An outer transaction remains caller owned. Synchronous SaveChanges refuses fences, observations, critical audit and the host's explicit async requirement. Security-sensitive admissions must require async; the legacy unfenced synchronous path is retained.

DatabaseLocks.LockAsync requires a current PostgreSQL transaction. DatabaseLockKeys preserves complete legacy key text; Create joins an owner-supplied namespace/components. No trimming, case change or new prefix is applied. Session DatabaseOperationLease uses a separate nonpooled connection and releases its lock by disposing that connection. It is distinct from transaction atomicity and a renewable fence. A secondary acquisition-cleanup failure adds only its type to the primary exception Data, preserving the primary exception.

ImmutableDocumentRegistry receives owner-scoped queries, neutral stamps, row mapping, canonical hashing and explicit admission/effect callbacks. It handles immutable retry/conflict behavior and pinned reads; schemas, events, scope codecs and frozen-view policy belong to the owner. AddDocumentRegistry maps the owner row without BaseEntity/AuditEntity inheritance. RegistryColumnNames makes legacy resource names explicit.

ReviewedConfigurationStore handles transactional revision updates and normalized proposal idempotency; ConfigurationApprovals handles independent-reviewer membership and accumulation. ConfigurationDriftGuard handles bounded scanning and rechecks before revocation. Hosts supply status/documents, roles, evidence/admission and the event factory; no provider topic or product producer is embedded in the engine.

The additive `ReviewedConfigurationStore.ChangeAsync` overload accepts an optional `admit` callback after the staging callback. Admission runs inside the newly opened transaction before the revision lock and state read, including idempotent retries. This lets a host acquire its earlier lock families without nesting transactions or copying the revision engine. Exceptions and cancellation propagate; no callback is retried, and transaction disposal leaves it uncommitted. The original signature delegates with no admission callback, preserving existing consumers. QuantCore opts in for its governance-before-configuration lock order; no product key is embedded in this library. NuGet consumers require a new Extensions release; compilation and executable concurrency/cancellation scenarios remain unrun.

TransactionalReadSet bounds observation identities, orders components ordinally and checks expiry/current-connection membership before invoking owner validation. Remember checks capacity before each observation; RememberEarliest bounds new identities and retains earliest expiry. The callback checks the actual authoritative row using the current transaction. Foreign-connection observations are skipped, not accepted as remote-service authority. DatabaseReadObservation keeps credentials internal and its diagnostic string redacted.

DatabaseActorScope owns explicit delegated/review/mutation scope lifetime, not authorization. The caller must authorize delegation/review first. AuditInterceptor keeps its original constructor and supports IAuditActorProvider/TimeProvider; default registration uses the current user and the existing Guid.Empty absent-actor convention. Resource owner selection is independent. Mutable audit columns and local immutable critical evidence are different mechanisms.

CriticalOperation<TTrace,TVersion,TEvidence> and CriticalOperationScope<T> preserve owner-supplied metadata contracts. CriticalAuditStager attaches a complete owner projection and returns its cleanup set; TrackedEntityFingerprint projects keys/values with an owner-supplied canonical hasher. Product ledgers, role policies, lineage graphs, model-state SQL, scope types, topics and identities remain outside this package.

No dependency, historical migration or entity inheritance is added. Builds, provider query/model comparisons, cancellation/concurrency and real-persistence checks have not run. A consuming context must opt into the shared base/plan; registering a fence does not intercept an arbitrary unrelated DbContext. Release/clean-package validation remains separate.
