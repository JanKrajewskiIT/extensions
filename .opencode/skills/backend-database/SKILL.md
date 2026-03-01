---
name: backend-database
description: Entity design, EF Core configuration, audit interceptor, user-scoping, PostgreSQL setup, data seeding, migrations, and NuGet package management
---

## Entity Hierarchy

- `BaseEntity` — abstract, provides `Guid Id` with `init` setter
- `AuditEntity : BaseEntity` — adds `CreatedBy`, `CreatedOn`, `ModifiedBy`, `ModifiedOn` (all `set`)
- Audit fields are auto-set by `AuditInterceptor` — never set manually in handlers

## Entity Property Conventions

- Use `required` for mandatory, `init` for immutable, `set` for mutable properties
- Collections: initialize as `= []`

## Entity Configuration

Configure entities in `IEntityTypeConfiguration<T>` classes using extension methods:

- `.ToBasicTable("Name")` — sets table name + Guid PK for `BaseEntity`
- `.ToAuditTable("Name")` — extends `ToBasicTable` + configures audit columns with `CURRENT_TIMESTAMP` defaults for `AuditEntity`
- Located in `Extensions.Infrastructure/Database/Entities/EntityConfigurationExtensions.cs`

## Data Type Constants

Use constants from `Extensions.Infrastructure/Database/Entities/`:

- `DataTypeConsts.NameLength` = 100 — for name/title columns
- `DataTypeConsts.TextLength` = 5000 — for description/text columns
- `PostgresCommandConsts.CurrentTimeStamp` = `"CURRENT_TIMESTAMP"` — for default SQL values
- `PostgresCommandConsts.JsonDataType` = `"jsonb"` — for JSON columns

## Database Setup

`AddPostgresDbContext<TContext>()` in `Extensions.Infrastructure/Database/DatabaseExtensions.cs` — registers DbContext with Npgsql, retry on failure (3 retries), and `AuditInterceptor`.

## Migrations

- `MigrationExtensions.MigrateDatabase<TContext>()` — checks for pending migrations, applies them, reloads Npgsql types
- Called in `RunModuleAsync`'s `beforeRun` callback
- Commands: `dotnet ef migrations add <Name>` and `dotnet ef database update` (run from service directory)

## Read-Only Queries

- Always `.AsNoTracking()` for read-only queries
- Project with `.Select()` — never return full entities from queries

## User Identity in Handlers

- `ICurrentUserService` (NOT `ICurrentUserContext`) — provides `UserId`, `UserName`, `Roles`, `IsInRole()`, `Claims`
- Implementation: `CurrentUserService` extracts claims from `HttpContext.User`
- Queries: `var userId = userService.UserId ?? Guid.Empty;`
- Commands: `var userId = userService.UserId ?? throw new UnauthorizedAccessException();`

## User-Scoping (IDOR Prevention)

Every handler operating on user data MUST filter by `CreatedBy == userId` to prevent IDOR. For related entities (e.g., Interview -> Offer), filter through the parent's `CreatedBy`.

## Data Seeding

- Seed data in `RunModuleAsync`'s `beforeRun` callback
- Questions module seeds 600+ questions from `Data/interview-questions.yaml` with deterministic GUIDs
- Categories use hierarchical parent-child structure, also seeded from YAML

## Central Package Management

NuGet versions are centralized in `Directory.Packages.props`. Shared build properties (TargetFramework, Nullable, etc.) live in `Directory.Build.props`. When adding a new NuGet package:
- Add `<PackageVersion Include="..." Version="..." />` to `Directory.Packages.props`
- In the `.csproj`, use `<PackageReference Include="..." />` **without** `Version`

## Key Files

- `Extensions.Domain/Entities/BaseEntity.cs` — `Guid Id` base entity
- `Extensions.Domain/Entities/AuditEntity.cs` — audit trail entity
- `Extensions.Domain/Abstractions/ICurrentUserService.cs` — current user abstraction
- `Extensions.Domain/Abstractions/Roles.cs` — `Admin`, `Moderator` role constants
- `Extensions.Infrastructure/Database/DatabaseExtensions.cs` — `AddPostgresDbContext<T>()` registration
- `Extensions.Infrastructure/Database/MigrationExtensions.cs` — `MigrateDatabase<T>()` helper
- `Extensions.Infrastructure/Database/Interceptors/AuditInterceptor.cs` — auto audit fields
- `Extensions.Infrastructure/Database/Entities/EntityConfigurationExtensions.cs` — `.ToBasicTable()`, `.ToAuditTable()`
- `Extensions.Infrastructure/Database/Entities/DataTypeConsts.cs` — string length constants
- `Extensions.Infrastructure/Database/Entities/PostgresCommandConsts.cs` — SQL constants
