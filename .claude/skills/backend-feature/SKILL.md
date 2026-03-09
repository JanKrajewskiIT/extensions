---
name: backend-feature
description: Vertical slice structure, CQRS abstractions, endpoint conventions, validation, error handling, service bootstrapping, and anti-patterns. Use when creating or modifying backend features that use Extensions.
---

## Vertical Slice Structure

`Features/{Feature}/{Action}/` with: `{Action}Endpoint.cs`, `{Action}Command.cs`/`Query.cs`, `{Action}Handler.cs`, `{Action}Request.cs`/`Response.cs`, `{Action}Validator.cs` (optional). Auto-registered via assembly scanning in `FeaturesExtensions.cs`.

## CQRS Abstractions (`Extensions.Application/Abstractions/`)

- `ICommand` -- no return | `ICommand<TResponse>` -- with return | `IQuery<TResponse>`
- `ICommandHandler<TCommand>` -> `Task` | `ICommandHandler<TCommand, TResponse>` -> `Task<TResponse>`
- `IQueryHandler<TQuery, TResponse>` -> `Task<TResponse>`

Commands: `record` + `ICommand`/`ICommand<TResponse>`. Queries: `record` + `IQuery<TResponse>`.
Handlers: always `sealed`, primary constructors for DI.

## Endpoints

Extension methods: `Map{Action}(this IEndpointRouteBuilder app)`.
Validation: manual `IValidator<TRequest>` inject or `.WithValidation<TRequest>()` filter.

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

- MediatR, repository/unit of work, skip `AsNoTracking()`, manual audit fields
- `ICurrentUserContext` (use `ICurrentUserService`), manual handler/validator registration
