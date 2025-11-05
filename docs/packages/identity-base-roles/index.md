# Identity.Base.Roles

## Overview
`Identity.Base.Roles` provides the shared role & permission infrastructure used by Identity Base hosts. It introduces EF Core entities for permissions (`Identity_Permissions`), roles (`Identity_Roles`), role-permission relationships, user-role assignments, and audit entries. The package also ships a seeding pipeline driven by configuration so you can declare permissions/roles in `appsettings.json` and have them synchronised automatically. `Identity.Base.Admin` and `Identity.Base.Organizations` rely on this package for consistent RBAC behaviour.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Roles
```

Register the package after `AddIdentityBase`:

```csharp
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Data;
using Microsoft.EntityFrameworkCore;

var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);

rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary"))); // or SQL Server, etc.

var app = builder.Build();
app.MapIdentityRolesUserEndpoints(); // exposes GET /users/me/permissions (optional)
await app.Services.SeedIdentityRolesAsync();
```

`AddIdentityRoles` returns an `IdentityRolesBuilder` that lets you register the built-in `IdentityRolesDbContext` (with bundled migrations) or point at an existing context implementing `IRoleDbContext`.

## Configuration

Two option sections drive the seed process:

| Section | Purpose | Example |
| --- | --- | --- |
| `Permissions` (`PermissionCatalogOptions`) | Canonical permission definitions (`Name`, optional `Description`). | `{ "Permissions": { "Definitions": [ { "Name": "users.read", "Description": "View users" } ] } }` |
| `Roles` (`RoleConfigurationOptions`) | Role definitions (`Name`, `Description`, `Permissions`, `IsSystemRole`) and default user/admin assignments. | `{ "Roles": { "Definitions": [ { "Name": "IdentityAdmin", "Permissions": ["users.read", "users.manage-roles"], "IsSystemRole": true } ], "DefaultUserRoles": ["StandardUser"], "DefaultAdminRoles": ["IdentityAdmin"] } }` |

Call `await app.Services.SeedIdentityRolesAsync()` during startup to synchronise the database with the configuration. The seeder is idempotent: it adds missing roles/permissions, updates descriptions, trims orphaned permissions, and invokes any registered role seed callbacks from `IdentityBaseBuilder.AfterRoleSeeding(...)`.

## Public Surface

| API / Type | Description |
| --- | --- |
| `AddIdentityRoles(this IServiceCollection, IConfiguration)` | Registers the RBAC services and returns an `IdentityRolesBuilder` for additional configuration. |
| `IdentityRolesBuilder.AddDbContext<TContext>()` | Registers a dedicated DbContext for RBAC storage (must implement `IRoleDbContext`). |
| `IdentityRolesBuilder.UseDbContext<TContext>()` | Reuses an existing DbContext registered elsewhere that implements `IRoleDbContext`. |
| `SeedIdentityRolesAsync(this IServiceProvider)` | Executes configuration-driven seeding; safe to call multiple times. |
| `IRoleSeeder` | Low-level service that inserts/upates role definitions. Used internally by the seeder. |
| `IRoleAssignmentService` | Helper for assigning/unassigning roles to users (`AssignRolesAsync`, `RemoveRolesAsync`, etc.). |
| `IPermissionResolver` | Core service used by Identity Base to build the effective permission claim. Combines database roles with any registered `IAdditionalPermissionSource`. |
| `IPermissionClaimFormatter` | Serialises resolved permissions into token claims (default format is space-delimited `identity.permissions`). |
| `MapIdentityRolesUserEndpoints()` | Registers `GET /users/me/permissions` – a debugging endpoint that returns `{ permissions: [...] }` for the current user. |

### Database schema
- `Identity_Roles` – role definition (`Name`, `Description`, `IsSystemRole`, timestamps).
- `Identity_Permissions` – permission catalog (`Name`, `Description`).
- `Identity_RolePermissions` – many-to-many join.
- `Identity_UserRoles` – link between Identity users (`AppDbContext`) and roles (composite key `{UserId, RoleId}`).
- `Identity_AuditEntries` – optional audit log used by the admin package.

## Extension Points

- **Additional permission sources** – implement `IAdditionalPermissionSource` to contribute dynamic permissions at token-issuance time. `Identity.Base.Organizations` uses this to append organization-specific permissions based on membership.
- **Permission claim formatting** – replace `IPermissionClaimFormatter` if you want the permission claim to use a different claim type or shape (e.g., JSON array instead of space-delimited string).
- **Custom storage** – implement `IRoleDbContext` in your application DbContext and call `UseDbContext<T>()` so RBAC tables live alongside the rest of your data.
- **Seed callbacks** – use `IdentityBaseBuilder.AfterRoleSeeding(...)` to run custom initialisation logic once seeding finishes (e.g., create default tenants or populate domain-specific roles).

## Dependencies & Compatibility
- Requires the core `Identity.Base` package.
- Consumed by `Identity.Base.Admin` and `Identity.Base.Organizations` to enforce permissions.
- Ships EF Core 9 migrations for `IdentityRolesDbContext`; apply them via `dotnet ef database update --project Identity.Base.Roles/Identity.Base.Roles.csproj --context Identity.Base.Roles.Data.IdentityRolesDbContext` or let your host run migrations automatically.

## Troubleshooting & Tips
- **Seed not running** – make sure `SeedIdentityRolesAsync` is called after the host has built the service provider (e.g., right before `RunAsync`).
- **Missing permissions in tokens** – confirm the user has the expected role via `IRoleAssignmentService` and inspect `GET /users/me/permissions` (requires `MapIdentityRolesUserEndpoints()`). Also verify no custom `IPermissionClaimFormatter` is stripping values.
- **Role removal** – seeding does not delete roles to avoid accidental data loss. Remove obsolete roles manually or add automation around `IRoleSeeder` if required.

## Examples & Guides
- [RBAC Design Reference](../../reference/rbac-design.md)
- [Identity Permissions Claim Guide](../../guides/identity-permissions-claim.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Playbook: ../../playbooks/seed-roles-and-default-organization.md

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Roles` entries)
