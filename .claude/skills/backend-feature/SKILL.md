---
name: backend-feature
description: Vertical slice structure, CQRS abstractions, endpoints, validation, error handling, bootstrapping. Use when creating or modifying features that use Extensions.
---

## Vertical Slice Structure

`Features/{Feature}/{Action}/` with: `{Action}Endpoint.cs`, `Command.cs`/`Query.cs`, `Handler.cs`, `Request.cs`/`Response.cs`, `Validator.cs` (optional). Auto-registered via assembly scanning.

## CQRS (`Extensions.Application/Abstractions/`)

- `ICommand` / `ICommand<TResponse>` / `IQuery<TResponse>`
- `ICommandHandler<T>` -> `Task` / `ICommandHandler<T, TResponse>` -> `Task<TResponse>`
- `IQueryHandler<TQuery, TResponse>` -> `Task<TResponse>`
- Commands/Queries: `record` types. Handlers: `sealed`, primary constructors.

## Endpoints

Extension methods: `Map{Action}(this IEndpointRouteBuilder app)`.
Validation: manual `IValidator<T>` inject or `.WithValidation<T>()` filter.

## Error Handling

`ValidationExceptionHandler` -> 422 `ValidationProblemDetails`.
`GlobalExceptionHandler`: `DataInconsistencyException`/`ArgumentException` -> 422, `InvalidOperationException` -> 409, `KeyNotFoundException` -> 404, `UnauthorizedAccessException` -> 403, `TimeoutException` -> 408, `NotImplementedException` -> 501, `HttpRequestException` (401/403) -> pass-through, others -> 500.

## Service Bootstrapping

```csharp
await ConfigurationExtensions.RunModuleAsync(args,
    configureServices: builder => { /* DbContext, handlers, HttpClients */ },
    configureApp: app => { /* map endpoints */ },
    beforeRun: async (services, ct) => { /* migrations, seeding */ });
```

`AddServiceDefaults()` auto-configures: Serilog, OpenTelemetry, service discovery, CORS, rate limiting, Keycloak JWT, OpenAPI, problem details, exception handlers, HybridCache, JSON serialization.

## AiResponseHelper (`Extensions.Infrastructure/Ai/`)

`CleanJsonResponse(string)` -- strips json fences. `DeserializeResponse<T>(string?)` -- clean + deserialize, throws `InvalidOperationException` -> 409.

## Anti-Patterns (NEVER)

MediatR, repository/unit of work, skip `AsNoTracking()`, manual audit fields, `ICurrentUserContext`, manual handler/validator registration.
