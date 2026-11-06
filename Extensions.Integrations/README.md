# Extensions.Integrations

Opt-in provider HTTP/JSON transport, bounded caches, quota admission and renewable request leases for .NET hosts. The package references Extensions.Domain only. It does not reference EF, ASP.NET, Jobs, a product DbContext or a broker SDK.

## Host composition

Supply an IHttpClientFactory-managed HttpClient, IProviderRequestGate, an explicit provider policy, and optional IProviderRequestExecutor/IProviderRequestObserver adapters to ProviderHttpTransport. The executor supplies the host budget token, a positive finite maximum request duration and the actual send operation. DirectProviderRequestExecutor is the explicit standalone default; it does not acquire a job budget. WithClient preserves the configured gate, policy, executor, telemetry category and pre-send admission hook while changing an existing managed client.

The host owns trusted endpoints, credentials, redirect policy and connection-time SSRF protection. Transport does not create HttpClient instances or add retries. Use the existing Infrastructure UrlSafetyValidator and a connection policy validating the actual resolved addresses for user-controlled/public-provider destinations. Unsafe writes must not gain retries from the client's handler chain.

Buffered responses enforce the byte limit against Content-Length and streamed reads. Both headers and the full buffered response have request deadlines, bounded by the client's timeout and the executor's maximum. Long-lived stream bodies use caller/budget cancellation and their owning lease; adapters supply idle/session limits and bounded JSON-lines/message sizes. JsonWebSocket retains only generic framing and JSON serialization; provider authentication and trusted websocket destinations remain host responsibilities.

## Quota authority

IProviderRequestPolicy.Resolve maps the host's priority vocabulary and canonical policy identity to ProviderRequestPolicy. Shared capacity excludes the reserved concurrency/minute/day capacity; Reserved requires explicit positive reserves. ProviderQuotaWindow is the single UTC-window calculation used by the local gate and an authoritative store adapter.

DistributedProviderRequestGate calls IProviderQuotaStore for atomic reservation, deferral, renewal and release. The store verifies the policy hash, applies approved policy changes, preserves counters and protects concurrent writes. It may be an owner-local database adapter or an authenticated client of the quota owner. It must honor cancellation and reject stale lease renewal. A consuming service must not copy another owner's counters or receive its database credentials.

Defaults in ProviderGateOptions retain a five-second queue budget, three-minute lease and ten-second cleanup budget. Renewal occurs every third of the lease duration and has a separate cancellation deadline of the same duration. Lease loss cancels LeaseLost, which the HTTP transport links into the request. A failed release is observed and leaves the lease to expire. Release returns concurrency only; minute/day attempts remain charged.

The process-local ProviderRequestGate is an explicit standalone option with at most 256 partitions. It is not selected as a fallback when the shared authority is unavailable. BoundedTtlCache has a bounded LRU index and per-entry expiration; no application cache is installed automatically.

## Failure and ownership contract

| Outcome | Behavior |
|---|---|
| Queue wait or capacity exceeds the queue budget | DependencyFailureException, code budget.provider_capacity_exhausted, retryable with delay. |
| HTTP 429 | Retain provider.rate_limited and bounded Retry-After; preserve that classification if storing the cooldown fails. |
| HTTP 408 / 5xx | ProviderFailures classifies a transient failure. |
| HTTP 401 / 403 | Non-retryable provider.access_denied. |
| HTTP 404 | Non-retryable provider.history_unavailable; adapters may first treat a lookup 404 as a normal absence. |
| Other non-success HTTP status | Non-retryable provider.request_rejected. |
| API error inside HTTP 200 | The provider adapter explicitly reads its documented body contract and maps it; transport does not guess response shapes. |
| Caller cancellation | Propagate cancellation without reclassifying it as provider failure. |
| Header/body deadline | TimeoutException; it is not evidence that an unsafe request was not received. |
| Lease loss | Cancel the request; preserve uncertain remote side effects for owner reconciliation. |
| Cleanup failure | Report through the observer without replacing the primary outcome. |

Buffered transport returns non-429 responses for the adapter to interpret; a streaming failure cannot return a successful stream. ProviderFailures is the common status classifier and creates coded DependencyFailureException metadata. Scheduler/HTTP adapters choose retry behavior and public error shape; the package has no job exception dependency. Retrying a dependency failure still requires the caller's deadline, budget and operation idempotency guarantees.

ProviderStreamingResponse owns the response and any linked lease/cancellation registration until DisposeAsync. The observer receives bounded host-selected dependency/category identities and clock observations, plus cleanup/failure types. Its implementation must not throw or expose credentials or bodies. A minimal trace observer is used when none is supplied.

The package is present in source only. No version has been published or changed, and no build, pack, restore, runtime or consumer check has run. Release must inspect the package and rebuild exact consumers from a clean package source.
