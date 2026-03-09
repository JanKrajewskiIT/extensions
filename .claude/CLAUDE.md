# Extensions

Shared .NET 11 library packages for Recron microservices:

- `Extensions.Application` -- CQRS abstractions (ICommand, IQuery, handlers), zero dependencies
- `Extensions.Domain` -- Entities, domain exceptions, user service abstraction
- `Extensions.Infrastructure` -- Database, auth, caching, observability, validation, cross-cutting

Dependency flow: API Projects -> Infrastructure -> Domain -> Application

## Commands

```bash
dotnet build --configuration Release  # build all (pre-commit check)
dotnet restore                        # restore NuGet packages
dotnet test                           # run tests
```

## Git

Branch from `main`, conventional commits: `feat:`, `fix:`, `refactor:`, `docs:`
Published to GitHub Packages via GitHub Actions on push to `main`.

## Structure

```
Extensions.Application/      # CQRS abstractions -- zero external deps
Extensions.Domain/           # Base entities, domain exceptions, abstractions
Extensions.Infrastructure/   # DI, EF Core, auth, caching, observability, validation
```

## Code Style

- XML doc comments on all public APIs
- Self-documenting code -- comment WHY not WHAT, no commented-out code
- No hardcoded secrets
