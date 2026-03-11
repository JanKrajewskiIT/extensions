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
- Commands/Queries: `record` types.

## Endpoints

Extension methods: `Map{Action}(this IEndpointRouteBuilder app)`.
Validation: manual `IValidator<T>` inject or `.WithValidation<T>()` filter.

## Error Handling

`ValidationExceptionHandler` -> 422. `GlobalExceptionHandler` maps domain exceptions to HTTP status codes -- see source for mapping.

## Service Bootstrapping

Bootstrap via `ConfigurationExtensions.RunModuleAsync(args, configureServices, configureApp, beforeRun)`.
`AddServiceDefaults()` auto-configures: Serilog, OpenTelemetry, service discovery, CORS, rate limiting, Keycloak JWT, OpenAPI, problem details, exception handlers, HybridCache, JSON serialization.

## AiResponseHelper (`Extensions.Infrastructure/Ai/`)

`CleanJsonResponse(string)` -- strips json fences. `DeserializeResponse<T>(string?)` -- clean + deserialize, throws `InvalidOperationException` -> 409.

