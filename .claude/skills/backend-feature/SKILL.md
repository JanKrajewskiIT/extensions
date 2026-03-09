---
description: Vertical Slice structure, CQRS commands/queries, endpoint conventions, validation, error handling, service bootstrapping, and anti-patterns for .NET backend features
---

## Vertical Slice Structure

Each feature lives in `Features/{Feature}/{Action}/` with these files:

- `{Action}Endpoint.cs` — minimal API endpoint (extension method on `IEndpointRouteBuilder`)
- `{Action}Command.cs` / `{Action}Query.cs` — `record` implementing `ICommand` / `IQuery<TResponse>`
- `{Action}Handler.cs` — `sealed` class with primary constructor, implements handler interface
- `{Action}Request.cs`, `{Action}Response.cs` — DTOs
- `{Action}Validator.cs` — FluentValidation `AbstractValidator<TRequest>` (optional)
- Handlers and validators are auto-registered via assembly scanning in `FeaturesExtensions.cs` — do NOT register them manually

## CQRS Abstractions

Located in `Extensions.Application/Abstractions/`:

- `ICommand` — marker for commands with no return value
- `ICommand<TResponse>` — marker for commands that return data
- `IQuery<TResponse>` — marker for queries
- `ICommandHandler<TCommand>` — handler returning `Task`
- `ICommandHandler<TCommand, TResponse>` — handler returning `Task<TResponse>`
- `IQueryHandler<TQuery, TResponse>` — handler returning `Task<TResponse>`

## CQRS Rules

- Commands: `record` implementing `ICommand` (no return) or `ICommand<TResponse>` (with return value)
- Queries: `record` implementing `IQuery<TResponse>`
- Use `ICommand<TResponse>` when a mutation must return data (e.g., AI calls that write and return results)
- Handlers: always `sealed`, use primary constructors for DI

## Endpoints

- Extension methods: `public static IEndpointRouteBuilder Map{Action}(this IEndpointRouteBuilder app)`
- Apply `.RequireAuthorization()`, `.WithName()`, `.WithTags()` for OpenAPI
- Two validation approaches:
  1. Manual injection: Inject `IValidator<TRequest>` and call `await validator.ValidateAndThrowAsync(request, ct)` before handler
  2. Automatic filter: Chain `.WithValidation<TRequest>()` on the route — uses `ValidationFilter<T>` (in `Extensions.Infrastructure/Validation/`) that auto-resolves the validator from DI
- Validators are auto-registered by `AddHandlersFromAssembly()` via `AddValidatorsFromAssembly()`

## Error Handling

Global exception handlers (registered via `AddExceptionHandler<T>` in `AddServiceDefaults`):

- `ValidationExceptionHandler` — catches `ValidationException`, groups errors by property name, returns 422 with `ValidationProblemDetails`
- `GlobalExceptionHandler` — catches everything else, maps to HTTP codes:
  - `DataInconsistencyException`, `ArgumentNullException`, `ArgumentException` → 422
  - `InvalidOperationException` → 409, `KeyNotFoundException` → 404
  - `UnauthorizedAccessException` → 403, `TimeoutException` → 408
  - `NotImplementedException` → 501, `HttpRequestException` (401/403) → pass-through
  - Others → 500
- In development: response includes `stackTrace` and `innerMessage`
- `DataInconsistencyException` in `Extensions.Domain/Exceptions/` — for business rule violations

## Service Bootstrapping

Every microservice uses `ConfigurationExtensions.RunModuleAsync()`:

```csharp
await ConfigurationExtensions.RunModuleAsync(
    args,
    configureServices: builder => { /* register DbContext, handlers, HttpClients */ },
    configureApp: app => { /* map endpoints */ },
    beforeRun: async (services, ct) => { /* run migrations, seed data */ }
);
```

`AddServiceDefaults()` automatically configures: structured logging (Serilog), OpenTelemetry metrics, service discovery, CORS, rate limiting, Keycloak JWT auth, OpenAPI with OAuth2, problem details, exception handlers, HybridCache, JSON serialization (enum as string).

## Backend Anti-Patterns (NEVER)

- NEVER use MediatR — custom CQRS only
- NEVER create repository/unit of work — use DbContext directly
- NEVER skip `AsNoTracking()` in queries
- NEVER manually set audit fields — `AuditInterceptor` does it
- NEVER use `ICurrentUserContext` — use `ICurrentUserService`
- NEVER manually register handlers or validators — assembly scanning does it

## AI Response Helper

For handlers that call an LLM and need to parse structured JSON from the response, use `AiResponseHelper` from `Extensions.Infrastructure/Ai/`:

- `AiResponseHelper.CleanJsonResponse(string)` — strips ` ```json ``` ` markdown fences from AI output
- `AiResponseHelper.DeserializeResponse<T>(string?)` — cleans + deserializes (case-insensitive), throws `InvalidOperationException` on empty/null or parse failure
- `GlobalExceptionHandler` maps `InvalidOperationException` → HTTP 409

## Key Files

- `Extensions.Application/Abstractions/` — ICommand, IQuery, handler interfaces
- `Extensions.Infrastructure/ConfigurationExtensions.cs` — `RunModuleAsync`, `AddServiceDefaults`, `UseCommonConfiguration`
- `Extensions.Infrastructure/Handlers/` — GlobalExceptionHandler, ValidationExceptionHandler
- `Extensions.Infrastructure/Validation/ValidationFilter.cs` — `ValidationFilter<T>` + `.WithValidation<T>()` extension
- `Extensions.Infrastructure/HandlerRegistrationExtensions.cs` — assembly scanning for CQRS handlers + FluentValidation
- `Extensions.Infrastructure/Ai/AiResponseHelper.cs` — JSON response parsing for LLM calls
