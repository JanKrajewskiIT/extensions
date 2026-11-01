# HTTP pipeline

Use AddServiceDefaults/RunModuleAsync for the existing host bootstrap. AddServiceDefaults registers common non-disclosing exception responses; opt into deadlines and an application mapper separately:

```csharp
using Extensions.Infrastructure;
using Extensions.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpPipeline<DefaultHttpExceptionMapper>();
builder.AddServiceDefaults(builder.Configuration.GetRequiredSection("Keycloak"));
var app = builder.Build();
app.UseRouting();
app.UseCommonConfiguration();
app.UseHttpRequestPipeline();
app.MapGet("/ping", () => Results.Ok()).RequireAuthorization();
await app.RunAsync();
```

A host replaces IRequestBudgetScopeFactory when it needs dependency attempt accounting or write fencing. It must begin ambient scopes synchronously and dispose them in ownership order. The linked deadline token replaces RequestAborted only while the fully awaited request is running. Caller cancellation remains cancellation; deadline cancellation becomes a coded business refusal. This is cooperative cancellation, not forced termination or proof of rollback. RequestBudgetLimit metadata overrides the positive five-minute default; routing must run first and exception handling must enclose this middleware. Neither registration nor RunModuleAsync installs these deadlines implicitly.

IHttpExceptionMapper supplies trusted machine codes, statuses, field path/code pairs and optional RetryAfter/AllowReadRetry. The shared writer clamps Retry-After, preserves correlation across exception handling and advertises retry only for transient reads. Never supply exception messages or provider response text as codes. Default mapping keeps Extensions argument/validation HTTP 422 and TimeoutException 408; QuantCore uses an explicit mapping to retain its 400/409/503 behavior.

The common JSON has type, title, status, code, traceId, correlationId, timestamp, retryable, validationErrors and fixed detail. Validator messages, attempted values, raw exception messages and stacks are not emitted by these handlers. The format replaces the former Extensions/ValidationProblemDetails response: adapting consumers or a major immutable release may be necessary. Existing Recron packages are not changed automatically; RunModuleAsync keeps its source signature.

To audit a route group, register a scoped IMutationAuditPolicy and add MutationAuditFilter explicitly. GET, HEAD and ReadOnlyOperation metadata bypass it. The host owns authorization, stronger-authentication evaluation, evidence gathering and durable critical receipts. IMutationAuditOperation must own its execution lifetime and await the effect; ObservedMutationAuditOperation enters its supplied ambient scope factory during execution and provides noncritical outcome audit and an independent ten-second cancellation budget, preserving the primary outcome on secondary audit failure. It is not transactional commit evidence.

HttpIdempotency.Require validates Idempotency-Key against a nonempty command GUID; it is not a durable deduplication store. AuthExtensions.PreserveAuthenticationMethods is opt-in and preserves validated AMR after the prior token-validation callback, without replacing JWT validation. UseStaticConsoleSecurityHeaders requires a host path and explicit CSP; it adds no global policy. ContractSchemas.Export accepts the publisher's types/options/transforms and never discovers other services' contracts.

QuantCore supplies its own execution/fence scope, critical-command audit policy and error mapper. A Recron API can continue RunModuleAsync with common error handling, or opt into deadlines with the default mapper and no product budget dependency. Scope and policy registration must remain host-owned; the library does not use service databases, Jobs or financial roles.

No builds, package operations, tests or runtime checks have been run for this source change.
