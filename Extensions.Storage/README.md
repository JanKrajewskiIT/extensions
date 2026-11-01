# Extensions.Storage

Opt-in versioned S3 storage for .NET 11. This source is unreleased; publication and consumer adoption are separate release work. It references Extensions.Domain, AWSSDK.S3 and Microsoft Options/DI/health abstractions, with no Jobs, EF, ASP.NET framework or product module dependency. Recron does not acquire S3 transitively through Extensions.Infrastructure.

## Registration

In an application host:

```csharp
using Extensions.Storage;

services.AddS3ArtifactStorage(options =>
{
    options.Bucket = configuration["ObjectStore:Bucket"]!;
    options.StoreName = configuration["ObjectStore:StoreName"]!;
    options.Region = configuration["ObjectStore:Region"]!;
    options.ServiceUrl = configuration["ObjectStore:ServiceUrl"];
    options.ForcePathStyle = configuration.GetValue<bool>("ObjectStore:ForcePathStyle");
});
services.AddHealthChecks().AddCheck<ObjectStoreDependencyHealthCheck>("object-store");
```

The host supplies configuration binding and health-check builder packages. Registration validates options at startup and never creates a bucket or IAM principal. One default store is registered per container. The owned AWS client is singleton; stores are scoped so a host budget executor may be scoped. The SDK has zero retries and a configurable 60-second timeout. Requests retain caller cancellation. Register a host IArtifactRequestExecutor before storage registration to attach the operation budget/telemetry. It runs one attempt and must not automatically repeat an uncertain write.

UseStaticCredentials defaults to false: the SDK credential chain is used and no static credential factory is called. An explicit static credential factory requires UseStaticCredentials=true; matching opt-in is validated. Supply credentials from the host secret provider, never options strings in tracked files. A custom ServiceUrl must be a trusted HTTPS origin with no user info, path, query or fragment; accepting configuration does not authorize a caller to choose a destination. Options are restart-bound, not hot reload.

## Ownership and JSON

ArtifactReference retains Store, ObjectKey, ObjectVersion, MediaType, Length and ContentHash. A reference is not an authorization grant. The host controls service-scoped credentials/namespaces and authorizes every read, write and retirement, including retention, entitlement and legal holds. Retention/entitlement VersionReference values are opaque metadata to this library; it does not infer or enforce product policies or configure a lifecycle deletion rule.

JsonArtifacts.WriteAsync requires an explicit IArtifactDependencyRecorder argument in addition to the store and policy versions. RawCapture.WriteAsync requires the same hook. There is no fallback no-op or automatic cast of the store to a recorder. Hosts map the exact artifact and payload into their own lineage data. Recorder failure propagates and does not return a successful result. The immutable object may already exist; the host must retain a durable write intent and recovery protocol around that external effect. No database transaction can undo an S3 write.

MarketData can wrap S3ArtifactStore with its owned data-use/lineage store and pass its dependency recorder. Intelligence can use its own document-owner wrapper and recorder over the same API. Neither adapter gives Storage an EF context, account scope, global dependency graph or job scheduler.

Canonical JSON uses Extensions.Domain.Serialization.PayloadSerializer. RawCapture preserves the original GetRawText UTF-8 bytes and raw/{provider}/{sha256}.json logical key; it does not canonicalize raw captures. S3 retains the content-addressed prefix {sha256}/ and the sha256, retention-policy and entitlement metadata fields. MaximumObjectBytes defaults to 1 GiB. Writes buffer to a delete-on-close temporary file, verify exact byte length/SHA-256 and confirm bucket versioning before upload. A missing/null returned version is rejected.

Reads and removal require an exact non-null content-addressed version in the configured store. Reads verify returned version and declared length, then check observed byte length and SHA-256 at EOF. The stream owns the SDK response; dispose it, consume to EOF for verification, and do not read concurrently. A failed integrity check is terminal. Early disposal does not prove integrity. JsonArtifacts.ReadAsync consumes EOF; BoundedJsonArtifacts.ReadTextAsync additionally enforces the caller byte budget, exact manifest hash/length and strict UTF-8, independently of the store implementation. It returns text; a caller that needs JSON parsing must parse it.

Retryable S3 throttling/server/transport outcomes are translated to DependencyFailureException with code artifact_store.unavailable and a five-second hint, retaining the cause. This is classification, not permission to repeat an unknown write or deletion. Host adapters map it to their application/job outcome and sanitize diagnostics. Bucket provisioning is a separate explicit ObjectStoreProvisioning.ProvisionAsync call using an authorized administrative client; runtime registration never invokes it.

## Deferred validation

No restore, build, pack, tests, bucket change or publication ran. The initial isolated command, when authorized, is:

```text
dotnet build Extensions.Storage/Extensions.Storage.csproj -c Release -p:GeneratePackageOnBuild=false
```

Run it from the Extensions root. Exact-version reads/deletion, corrupted/truncated streams, cancellation, credential-chain behavior, IAM isolation, duplicate write recovery, package contents and clean consumers require separate release/integration evidence. Do not reuse a published package version.
