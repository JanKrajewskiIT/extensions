# Extensions

Shared .NET 11 library for Recron microservices (3 NuGet packages via GitHub Packages):

- `Extensions.Application` -- CQRS abstractions (ICommand, IQuery, handlers), zero deps
- `Extensions.Domain` -- Base entities, domain exceptions, user service abstraction
- `Extensions.Infrastructure` -- EF Core, auth, caching, observability, validation

Dependency flow: API Projects -> Infrastructure -> Domain -> Application

## Commands

```bash
dotnet build --configuration Release  # build all (pre-commit)
dotnet restore                        # restore NuGet
dotnet test                           # run tests
```

## Git

Branch from `main`, conventional commits (`feat:`, `fix:`, `refactor:`, `docs:`).
Published via GitHub Actions on push to `main`.

## Code Style

- Self-documenting -- comment WHY not WHAT, no commented-out code, no hardcoded secrets
