# AGENTS.md — Recron

Full-stack app: .NET 11 backend (Custom CQRS, Vertical Slices, Clean Architecture) + SolidJS frontend (TypeScript, CSS Modules, TanStack Solid Query). Orchestrated by .NET Aspire. Auth via Keycloak OIDC. Database: PostgreSQL + EF Core 11.

## Skills Reference

Detailed conventions are available as on-demand skills. Load them when working on a specific area:

| Skill | When to load |
|---|---|
| `backend-feature` | Creating/modifying backend features (vertical slices, CQRS, endpoints, error handling) |
| `backend-database` | Entity design, EF Core config, audit fields, user-scoping, NuGet packages |
| `ai-integration` | AI/LLM features: IChatClient, prompt patterns, AiResponseHelper, frontend AiResultPanel |

## Build & Run Commands

### Backend (.NET)

```bash
dotnet restore                                          # restore NuGet packages
dotnet build --configuration Release                    # build all projects
dotnet test                                             # run all tests (none exist yet)
cd Recron.AppHost && dotnet run                         # start full stack via Aspire
cd Questions.Api && dotnet build                        # build single service
cd Questions.Api && dotnet ef migrations add <Name>     # add EF migration
cd Questions.Api && dotnet ef database update           # apply migrations
```

### Pre-commit Checklist

Always run before committing: `cd recron-web && npx tsc --noEmit && npm run lint && npm run build`
Backend: `dotnet build --configuration Release`

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
