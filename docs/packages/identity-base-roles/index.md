# Identity.Base.Roles

## Overview
`Identity.Base.Roles` delivers the role and permission subsystem that complements the core Identity Base package. It introduces EF Core models for roles, permissions, assignments, and audit entries; configuration-driven seeding; and helper services for resolving effective permissions. `Identity.Base.Admin` and `Identity.Base.Organizations` depend on this package to provide consistent RBAC behaviour.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Roles
```

Register the package once `Identity.Base` has been added:

```csharp
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Data;
using Microsoft.EntityFrameworkCore;

var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);

// Use the built-in DbContext (PostgreSQL example)
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

// -- OR -- point at an existing DbContext implementing IRoleDbContext
// rolesBuilder.UseDbContext<AppDbContext>();

var app = builder.Build();
app.MapIdentityRolesUserEndpoints();          // optional Minimal APIs
await app.Services.SeedIdentityRolesAsync();  // ensure roles/permissions are provisioned
```

## Configuration

The package binds two primary option objects:
- `PermissionCatalogOptions` – the canonical set of permissions and metadata.
- `RoleConfigurationOptions` – system role definitions (name, description, permissions) plus default assignments for users/admins.

Populate them in configuration (e.g., `appsettings.json`) or via `services.Configure<PermissionCatalogOptions>(...)`. During startup, `SeedIdentityRolesAsync` reads the definitions, writes any missing roles/permissions, and invokes registered callbacks.

## Public Surface

| API / Type | Description |
| --- | --- |
| `AddIdentityRoles(this IServiceCollection, IConfiguration)` | Entry point returning an `IdentityRolesBuilder` for additional configuration. |
| `IdentityRolesBuilder.AddDbContext<TContext>()` | Registers the provided DbContext as the RBAC store. |
| `IdentityRolesBuilder.UseDbContext<TContext>()` | Reuses an existing DbContext that implements `IRoleDbContext`. |
| `SeedIdentityRolesAsync(this IServiceProvider)` | Executes seeding for roles, permissions, and default assignments. |
| Services: `IRoleSeeder`, `IRoleAssignmentService`, `IPermissionResolver`, `IPermissionClaimFormatter` | Consumed by hosts, admin APIs, and the organizations package to manage RBAC state and construct permission claims. |
| Minimal APIs: `MapIdentityRolesUserEndpoints()` | Exposes `/roles` endpoints for querying role metadata (used by SPAs). |

## Extension Points

- Implement `IAdditionalPermissionSource` to augment the permission claim (Identity.Base.Organizations uses this to append organization-scoped permissions).
- Replace `IPermissionClaimFormatter` to change how permissions are serialized into tokens.
- Register seed callbacks (`IdentityRolesBuilder.AfterRolesSeed(...)`) to run custom provisioning after the built-in seeding completes.
- Swap storage providers by supplying your own `IRoleDbContext`.

## Dependencies & Compatibility

- Requires `Identity.Base`.
- Feeds `Identity.Base.Admin` (admin API) and `Identity.Base.Organizations` (organization role overrides).
- Ships EF Core 9 migrations for `IdentityRolesDbContext`; run via `dotnet ef database update` or rely on hosted migration services in downstream packages.

## Examples & Guides

- [RBAC Design Reference](../../reference/rbac-design.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Roles` entries)
