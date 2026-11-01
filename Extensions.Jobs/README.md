# Extensions.Jobs

Opt-in durable PostgreSQL jobs, recurring schedules, renewable leases and execution budgets for .NET hosts. The source package uses the existing Extensions.Messaging/Infrastructure dependency graph; it introduces no external package or service dependency. Infrastructure does not reference Jobs, so its existing consumers do not acquire Jobs or Kafka implicitly.

## Composition

Call `services.AddJobs()` to register TimeProvider (if absent) and the typed `ScopedJobOperation<TService>` lifetime boundary. This starts no worker and configures no database. Call `AddJobHandlersFromAssembly(assembly, include)` to discover scoped `IJobHandler<TJob>` implementations; the optional predicate limits discovery to the host's supported capabilities. Registration does not infer authorization or job-kind routing.

The owner registers its DbContext, `IJobCodec<TJob>`, `IJobPersistenceEvents<TJob>`, `EfJobStore<TContext,TJob>`, `IJobPersistence<TJob>` and `IJobWorker<TJob>`, then explicitly hosts `JobRunner<TJob>`. Bind and validate JobRunnerOptions with `JobRunnerOptions.IsValid`. The worker maps kind to a handler, binds the actor/audit scope, applies admission and classifies failures. The codec owns scope keys, canonical payloads and state-machine adaptation; events stage the owner's attempt journal and completion fact in the supplied transaction. No topic, producer, product origin, priority or credential is inferred by Jobs.

Compose `modelBuilder.AddJobs()` in the owner's model for jobs and recurring definition/state rows. Compose Messaging's `AddOutboxInbox()` in the same context for transactional admission/publication, and `AddMessageQuarantine()` only where needed. The context's default schema determines these tables. The service owns its migrations and credentials; the package contains no migrations, connection string or shared business database. The generic store requires PostgreSQL transaction advisory locks and JSONB support.

## Execution

JobDefinition/JobCheckpoint/JobSnapshot are generic over owner metadata; DurableJob supplies the fenced state machine. JobResult carries the owner's serialized result reference, not a storage client. The runner claims with the existing concurrency and per-job transaction locks, preserves increasing fences, and renews/checkpoints/completes through a fresh typed persistence scope with optimistic versions. Acceptance, inbox deduplication and attempt start commit together; completion hooks/outbox commit with state. Hosts must keep handler business effects idempotent independently of job completion.

For recurring work register `IRecurringScheduleCodec<TSchedule,TLease>`, `EfRecurringScheduleSource`, `EfJobDependencies`, `RecurringScheduleStore`, `IRecurringPersistenceEvents` and an explicit `IRecurringJobRuntime<TSchedule,TLease,TJob>`. Host `RecurringJobScheduler<TSchedule,TLease,TJob>` with its typed operation factory. Each operation gets a fresh scoped runtime/context; the scheduler and budget engine never resolve arbitrary services. RecurringJobMaterializer receives typed prepare/create callbacks. Immutable definitions, lease identity, current-definition checks, dependency waits, bounded catch-up, occurrence IDs and inbox/job/schedule transaction remain separate from owner-specific rolling-input generation.

The source joins latest definitions to state, filters future `NextRunAt` and active leases before acquisition, and checks the effective first time of schedules without state. Catch-up stops when no occurrence commits or a dependency is missing. When a definition changes, `RecurringScheduleTiming.NextRunAt` retains the later of previous progress and the new `FirstRunAt`; version edits do not replay an advanced history. The owner codec must validate semantic rules and the immutable fingerprint.

`JobRow.CreatedAt` is populated by `JobRows.Create` and stays immutable across state writes. The model now requires this column plus the owner/kind/creation/ID and dependency-scope/status/creation indexes. `EfJobDependencies` uses SQL existence checks over creation time without decoding payloads or limiting candidates. Its existing constructor accepting a codec remains available for source/binary compatibility; the codec is no longer needed by the query. Each consuming host owns a migration and an accurate backfill using its own payload format. Deploy the schema before compatible hosts and coordinate old writers, which cannot supply the new required column.

## Work budgets

`SharedWorkBudgets<TRequest,TAllocation>` reserves and settles through `IWorkBudgetStore<TRequest,TAllocation>`; allocations implement IWorkAllocation. The store owns the atomic shared partition, approval, active allocations, operation counters and durable receipts. It may use the owner's existing PostgreSQL registry without adding a new generic table. WorkCapacity supplies neutral capacity, deadline and charge arithmetic; the host maps its own priority to the protected-capacity flag.

WorkExecutor composes the inherited execution budget, reservation token and database write fence. It tracks late tasks and defers settlement until they finish. Expiry does not automatically free an uncertain allocation; failed settlement or tracking overflow requires owner reconciliation. LocalWorkBudget supports a host-provided exhaustion exception factory. Scope/origin, research/live classification, approved policy, admin evidence and cleanup authority stay outside this package.

All workers sharing a limit must use one authoritative store/partition. Cloning counters in service-local job databases changes the limit. An isolated service can own a local pool; a platform-wide pool needs one owner and authenticated admission/settlement calls. The library grants no foreign-database access.

## Compatibility and deferred checks

This source extraction preserves the existing QuantCore state transitions, enum values, lock/occurrence identities. The creation-time column/index addition and revised schedule timing are described above. Canonical JSON and EF metadata comparisons, lease/retry/cancellation/recovery scenarios, package packing and clean-consumer builds remain unrun. Known claim candidate-window, long-deadline and UTC-day-boundary behavior is retained for the separate defect task. Moving enums/contracts and deriving closed records changes CLR contracts and requires consumer rebuilds and release classification. No version was changed, package published, database migrated or runtime started.
