---
paths:
  - "**/*.cs"
---

# C# Rules

- Handlers: `sealed`, primary constructors for DI
- Queries: `AsNoTracking()` + `Select()` projections
- Entities: `BaseEntity` or `AuditEntity`, never set audit fields manually
- `ICurrentUserService` for identity (not `ICurrentUserContext`)
- XML doc comments on all public types/methods/properties
- No MediatR, no repository pattern, no unit of work
- `DataInconsistencyException` for business rule violations
- Third-party deps only in Extensions.Infrastructure
