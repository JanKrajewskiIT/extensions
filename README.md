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

The pending 1.9.0 release includes the new `AddHostDefaults` API and the `AiRequestExecutor` overload accepting caller-owned `ChatOptions`; neither is present in the cached 1.8.1 package used by Recron. `Directory.Build.props` owns the shared package version. QuantCore now consumes `Extensions.Infrastructure` from GitHub Packages, as Recron does, and requires 1.9.0 to be published first. Release requires separate pack/consumer verification of all three packages; no package was published or existing version replaced. Recron's package selection is unchanged.
