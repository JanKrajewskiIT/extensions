---
name: nuget-package
description: Package responsibilities, versioning, backward compatibility, XML docs, publishing. Use when extending packages or preparing a release.
---

## Package Responsibilities

```
Extensions.Application    <- zero external deps, pure CQRS abstractions
Extensions.Domain         <- may reference Application
Extensions.Infrastructure <- may reference Domain + Application, all third-party deps
```

## Belongs in Extensions If

- No Recron-specific business logic, operates on abstractions
- Same pattern in 2+ services
- Usable in any .NET + PostgreSQL + EF Core project

## XML Docs (required)

`<summary>` on every public member. `<typeparam>`, `<param>`, `<returns>`, `<exception cref>` as applicable.

## Versioning

All three packages share `1.0.PATCH`. Bump all three `.csproj` together.
Consumers update `Directory.Packages.props`.

## Backward Compatibility

Non-breaking: new interfaces, optional overloads, extension methods, constants.
Breaking: removing/renaming members, changing signatures -- coordinate first.
When in doubt: add overload, don't change existing.

## Adding Infrastructure Extensions

```csharp
/// <summary>Registers <typeparamref name="T"/> in the DI container.</summary>
public static IServiceCollection AddSomething<T>(this IServiceCollection services) where T : class
{ /* impl */ return services; }
```

Add to `AddServiceDefaults()` only if every microservice needs it. Otherwise opt-in.

## Publishing

Automated via `.github/workflows/publish-nuget.yml` on push to `main` or `v*` tag.
Pipeline: restore -> build Release -> pack -> push to GitHub Packages (`--skip-duplicate`).

## Pre-publish Checklist

- `dotnet build --configuration Release` -- zero warnings
- All public members have XML docs
- Version bumped in all three `.csproj`
- Third-party deps only in Infrastructure
