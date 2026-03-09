---
name: backend-database
description: Entity design, EF Core configuration, audit interceptor, user-scoping, PostgreSQL setup, migrations, and data seeding. Use when working with database entities or EF Core infrastructure.
---

## Entity Hierarchy

`BaseEntity` (abstract, `Guid Id` init) -> `AuditEntity` (adds `CreatedBy`, `CreatedOn`, `ModifiedBy`, `ModifiedOn`). Audit fields auto-set by `AuditInterceptor`.

## Property Conventions

`required` mandatory, `init` immutable, `set` mutable. Collections: `= []`.

## Entity Configuration

`IEntityTypeConfiguration<T>` with:
- `.ToBasicTable("Name")` -- table + Guid PK
- `.ToAuditTable("Name")` -- + audit columns + `CURRENT_TIMESTAMP` defaults

Constants: `DataTypeConsts.NameLength` 100, `.TextLength` 5000, `PostgresCommandConsts.CurrentTimeStamp`, `.JsonDataType` "jsonb".

## Database Setup

`AddPostgresDbContext<TContext>()` -- Npgsql (3 retries) + AuditInterceptor. Register PostgreSQL enums in Npgsql options.

## Migrations

`MigrationExtensions.MigrateDatabase<TContext>()` in `beforeRun`. Commands: `dotnet ef migrations add <Name>` / `dotnet ef database update`.

## Queries & User Identity

Always `AsNoTracking()` + `Select()`. `ICurrentUserService`: `UserId`, `UserName`, `Roles`, `IsInRole()`.
IDOR: filter `CreatedBy == userId`. Deterministic GUID seeding for idempotent seeds.

## Key Files

- `Extensions.Domain/Entities/` -- BaseEntity, AuditEntity
- `Extensions.Domain/Abstractions/` -- ICurrentUserService, Roles
- `Extensions.Infrastructure/Database/` -- DatabaseExtensions, MigrationExtensions, AuditInterceptor
- `Extensions.Infrastructure/Database/Entities/` -- EntityConfigurationExtensions, DataTypeConsts, PostgresCommandConsts
