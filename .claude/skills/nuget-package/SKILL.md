---
name: nuget-package
description: Adding public APIs, versioning, backward compatibility, XML doc requirements, and publishing to GitHub Packages. Use when extending any Extensions package or preparing a release.
---

## Package Responsibilities

```
Extensions.Application    <- zero external deps, pure CQRS abstractions
Extensions.Domain         <- may reference Application
Extensions.Infrastructure <- may reference Domain + Application, all third-party deps here
```

## Belongs in Extensions If

- No Recron-specific business logic, operates on abstractions
- Same pattern in 2+ services
- Could be used in any .NET + PostgreSQL + EF Core project

## XML Doc Comments (required on all public APIs)

`<summary>` on every public member. `<typeparam>`, `<param>`, `<returns>`, `<exception cref>` as applicable. No implementation comments.

## Versioning

All three packages share `1.0.PATCH`. Bump all three `.csproj` together:

```xml
<Version>1.0.15</Version>  <!-- increment from current -->
```

Consumers update `Directory.Packages.props`:
```xml
<PackageVersion Include="Extensions.Infrastructure" Version="1.0.15" />
```

## Backward Compatibility

Non-breaking (safe): new interfaces, optional overloads, extension methods, constants.
Breaking (coordinate): removing/renaming members, changing signatures.
When in doubt: add overload, don't change existing.

## Adding Infrastructure Extensions

```csharp
/// <summary>Registers <typeparamref name="T"/> in the DI container.</summary>
public static IServiceCollection AddSomething<T>(this IServiceCollection services) where T : class
{ /* impl */ return services; }
```

Add to `AddServiceDefaults()` only if every microservice needs it. Otherwise leave as opt-in.

## Publishing

Automated via `.github/workflows/publish-nuget.yml` on push to `main` or `v*` tag.
Pipeline: restore -> build Release -> pack -> push to GitHub Packages (`--skip-duplicate`).

## Pre-publish Checklist

- `dotnet build --configuration Release` passes with zero warnings
- All new public members have XML doc comments
- Version bumped in all three `.csproj` files
- Third-party deps only in Extensions.Infrastructure
