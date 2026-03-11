# Extensions

Shared .NET 11 library for Recron microservices — CQRS abstractions, domain building blocks, and infrastructure setup. Published as 3 NuGet packages via GitHub Packages.

Full documentation: [Recron Docs → Technical → Shared Packages](https://docs.bluebraces.online/technical/shared-packages/) | [Recron Docs → External → Extensions](https://docs.bluebraces.online/external/extensions/)

## Packages

| Package | Purpose |
|---------|---------|
| `Extensions.Application` | CQRS abstractions (`ICommand`, `IQuery`, handlers) |
| `Extensions.Domain` | Base entities, domain exceptions |
| `Extensions.Infrastructure` | EF Core, auth, caching, observability, validation |

## Install

```xml
<PackageReference Include="Extensions.Infrastructure" Version="1.0.*" />
```

`nuget.config` for GitHub Packages authentication:

```xml
<packageSources>
  <add key="github" value="https://nuget.pkg.github.com/JanKrajewskiIT/index.json" />
</packageSources>
```
