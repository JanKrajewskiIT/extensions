# AGENTS.md — Extensions

Shared .NET 11 library packages used across Recron microservices:

- `Extensions.Application` — CQRS abstractions (ICommand, IQuery, handlers), zero dependencies
- `Extensions.Domain` — Entities, value objects, domain exceptions, user service abstraction
- `Extensions.Infrastructure` — Database, auth, caching, observability, validation, cross-cutting concerns

Dependency flow: API Projects → Extensions.Infrastructure → Extensions.Domain → Extensions.Application

## Skills Reference

Detailed conventions are available as on-demand skills. Load them when working on a specific area:

| Skill | When to load |
|---|---|
| `backend-feature` | Creating/modifying backend features (vertical slices, CQRS, endpoints, error handling) |
| `backend-database` | Entity design, EF Core config, audit fields, user-scoping |

## Build & Run Commands

```bash
dotnet restore                        # restore NuGet packages
dotnet build --configuration Release  # build all projects
dotnet test                           # run all tests (none exist yet)
```

### Pre-commit Checklist

Always run before committing: `dotnet build --configuration Release`

### Git Conventions

- Branch from `main`, use conventional commits: `feat:`, `fix:`, `refactor:`, `docs:`

## Project Structure

```
Extensions.Application/      # CQRS abstractions (ICommand, IQuery, handlers) — zero deps
Extensions.Domain/           # Entities, value objects, domain exceptions
Extensions.Infrastructure/   # Database, auth, caching, observability, cross-cutting
```

Dependency flow: API Projects -> Extensions.Infrastructure -> Extensions.Domain -> Extensions.Application

## Security & Comments

- Never hardcode secrets — use environment variables.
- Code should be self-documenting — comment WHY, not WHAT. No commented-out code.
- Frontend: no comments at all — code must be self-documenting.
- XML doc comments for public APIs (backend).
