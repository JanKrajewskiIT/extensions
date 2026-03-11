---
name: nuget-package
description: Package responsibilities, versioning, backward compatibility, XML docs, publishing. Use when extending packages or preparing a release.
---

## Package Responsibilities

`Application` (zero deps) <- `Domain` <- `Infrastructure` (all third-party deps).

## Belongs in Extensions If

- No Recron-specific business logic, operates on abstractions
- Same pattern in 2+ services
- Usable in any .NET + PostgreSQL + EF Core project

## XML Docs

`<summary>` on every public member. `<typeparam>`, `<param>`, `<returns>`, `<exception cref>` as applicable.

## Versioning

All three packages share `1.0.PATCH`. Bump all three `.csproj` together.
Consumers update `Directory.Packages.props`.

## Backward Compatibility

Non-breaking: new interfaces, optional overloads, extension methods, constants.
Breaking: removing/renaming members, changing signatures -- coordinate first.
When in doubt: add overload, don't change existing.

## Adding Infrastructure Extensions

Add to `AddServiceDefaults()` only if every microservice needs it. Otherwise opt-in.

## Publishing

Automated via `.github/workflows/publish-nuget.yml` on push to `main` or `v*` tag.
Pipeline: restore -> build Release -> pack -> push to GitHub Packages (`--skip-duplicate`).

## Pre-publish Checklist

- `dotnet build --configuration Release` -- zero warnings
- All public members have XML docs
- Version bumped in all three `.csproj`
