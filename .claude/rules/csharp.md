---
paths:
  - "**/*.cs"
---

# C# Code Rules

- Handlers: `sealed`, primary constructors for DI
- Queries: always `AsNoTracking()`, project with `Select()`
- Entities: `BaseEntity` or `AuditEntity`, never set audit fields manually
- `ICurrentUserService` for user identity (not `ICurrentUserContext`)
- XML doc comments on all public types, methods, properties
- No MediatR, no repository pattern, no unit of work
- Use `DataInconsistencyException` for business rule violations
- NuGet: third-party deps only in Extensions.Infrastructure
- Self-documenting code, no hardcoded secrets
