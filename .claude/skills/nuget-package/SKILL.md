---
description: Adding new public APIs to Extensions packages, versioning, backward compatibility, XML docs requirements, and publishing to GitHub Packages. Use when extending Extensions.Application, Extensions.Domain, or Extensions.Infrastructure.
---

## Package Responsibilities

Respect the dependency flow — lower packages must never reference upper ones:

```
Extensions.Application   ← zero external dependencies, pure abstractions
Extensions.Domain        ← may reference Extensions.Application
Extensions.Infrastructure ← may reference Extensions.Domain + Extensions.Application
```

**What belongs where:**

- `Extensions.Application` — CQRS marker interfaces and handler contracts only. No implementation, no external NuGet deps.
- `Extensions.Domain` — Base entities (`BaseEntity`, `AuditEntity`), domain abstractions (`ICurrentUserService`), role constants (`Roles`), domain exceptions (`DataInconsistencyException`).
- `Extensions.Infrastructure` — DI registration extensions, EF Core infrastructure, middleware, interceptors, cross-cutting helpers (`AiResponseHelper`). This is where all third-party NuGet deps live.

## Signals That Code Belongs in Extensions

- No Recron-specific business logic — operates only on abstractions
- Same pattern appears in 2+ Recron services
- Could be used in any .NET + PostgreSQL + EF Core project

## Adding New Public APIs

Every public type, method, and property requires XML doc comments:

```csharp
/// <summary>
/// Marks a request as a query that returns <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type returned by the query handler.</typeparam>
public interface IQuery<TResponse>;
```

Rules:
- `<summary>` on every public member — one-line preferred
- `<typeparam>` for every generic parameter
- `<param>` for every method parameter
- `<returns>` for non-void methods
- `<exception cref="T">` for documented throws
- No implementation comments — XML docs describe intent, not mechanics

## Versioning

All three packages share the same version. When adding or changing any public API, bump **all three** `.csproj` files together:

```xml
<!-- Extensions.Application/Extensions.Application.csproj -->
<!-- Extensions.Domain/Extensions.Domain.csproj -->
<!-- Extensions.Infrastructure/Extensions.Infrastructure.csproj -->
<Version>1.0.15</Version>  <!-- bump from 1.0.14 -->
```

Version convention: `1.0.PATCH` — increment patch for all changes (this is an internal library, no semver enforcement needed).

Consumers update by changing `Directory.Packages.props` in their repository:
```xml
<PackageVersion Include="Extensions.Infrastructure" Version="1.0.15" />
```

## Backward Compatibility

This library is consumed by Recron microservices. Breaking changes require coordinated updates:

- **Non-breaking (safe to add):** new interfaces, new optional method overloads, new extension methods, new constants
- **Breaking (coordinate with Recron):** removing or renaming public members, changing method signatures, removing packages/interfaces that consumers depend on
- When in doubt, add a new overload rather than changing an existing signature

## Adding a New Infrastructure Extension

Pattern for new `AddSomething<T>()` extension in `Extensions.Infrastructure`:

```csharp
/// <summary>
/// Registers <typeparamref name="T"/> and its dependencies in the DI container.
/// </summary>
public static IServiceCollection AddSomething<T>(this IServiceCollection services)
    where T : class
{
    // implementation
    return services;
}
```

Register the extension in `AddServiceDefaults()` in `ConfigurationExtensions.cs` only if it should apply to every microservice. Otherwise leave it as an opt-in extension method.

## Publishing

Publishing is fully automated via `.github/workflows/publish-nuget.yml` — triggered on push to `main` or version tag `v*`.

The pipeline:
1. `dotnet restore Extensions.slnx`
2. `dotnet build --configuration Release`
3. `dotnet pack` each project → `.nupkg`
4. `dotnet nuget push` to `https://nuget.pkg.github.com/JanKrajewskiIT/index.json`

**No manual publish steps needed.** Just merge to `main` and the packages are published.

To verify published packages: check the GitHub repository's Packages page or update `Directory.Packages.props` in Recron and run `dotnet restore`.

## Pre-publish Checklist

```bash
dotnet build --configuration Release   # must pass with zero warnings
```

- All new public members have XML doc comments
- Version bumped in all three `.csproj` files
- No hardcoded secrets or environment-specific values
- New dependencies added to the correct project only (third-party deps → Extensions.Infrastructure only)

## Key Files

- `Extensions.Application/Extensions.Application.csproj` — version
- `Extensions.Domain/Extensions.Domain.csproj` — version
- `Extensions.Infrastructure/Extensions.Infrastructure.csproj` — version + NuGet deps
- `Extensions.Infrastructure/ConfigurationExtensions.cs` — `AddServiceDefaults` pipeline
- `.github/workflows/publish-nuget.yml` — CI/CD publishing
- `nuget.config` — GitHub Packages source configuration
