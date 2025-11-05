# Identity.Base.Admin

## Overview
`Identity.Base.Admin` layers administrative APIs on top of Identity Base and the RBAC package. It exposes authenticated `/admin` endpoints for user lifecycle management, role definition management, and permission lookups. All endpoints enforce admin-specific permission requirements (e.g., `users.read`, `roles.manage`) and are intended for internal back-office tooling or dedicated admin applications.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Admin
```

Add the services after registering Identity Base. `AddIdentityAdmin` internally calls `AddIdentityRoles` and returns the underlying `IdentityRolesBuilder`, so you can continue configuring role storage on the returned builder:

```csharp
using Identity.Base.Admin;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Data;
using Microsoft.EntityFrameworkCore;

var rolesBuilder = builder.Services.AddIdentityAdmin(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var app = builder.Build();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
await app.Services.SeedIdentityRolesAsync();
```

`MapIdentityAdminEndpoints()` registers the `/admin/users`, `/admin/roles`, and `/admin/permissions` Minimal APIs.

## Configuration

- `AdminApiOptions` – paging limits, search throttles, default sort orders exposed to the admin endpoints.
- `AdminDiagnosticsOptions` – slow-query logging thresholds and metrics tags.
- `AdminAuthorizationOptions` – configure how admin permissions map to scopes/claims (`RequireAdminPermission(...)`).
- `DatabaseOptions` (from Identity Base) continue to drive storage; the admin package reuses the same user store and RBAC context.

## Public Surface

### Minimal API Endpoints

| Route | Description | Required Permission |
| --- | --- | --- |
| `GET /admin/users` | Paged user list with filtering (search, role, lockout state). | `users.read` |
| `GET /admin/users/{id}` | Detailed user profile including roles and MFA state. | `users.read` |
| `POST /admin/users` | Create a user and optionally assign roles/send invite email. | `users.create` |
| `PUT /admin/users/{id}` | Update profile flags (display name, email, email confirmation, lockout). | `users.update` |
| `POST /admin/users/{id}/lock` / `/unlock` | Lock or unlock an account. | `users.lock` |
| `POST /admin/users/{id}/force-password-reset` | Generate reset token and send email. | `users.reset-password` |
| `POST /admin/users/{id}/mfa/reset` | Disable MFA and reset authenticator keys. | `users.reset-mfa` |
| `POST /admin/users/{id}/resend-confirmation` | Resend confirmation email if pending. | `users.update` |
| `GET /admin/users/{id}/roles` | List current role assignments. | `users.manage-roles` |
| `PUT /admin/users/{id}/roles` | Replace assigned roles. | `users.manage-roles` |
| `DELETE /admin/users/{id}` | Soft-delete (quarantine) a user. | `users.delete` |
| `POST /admin/users/{id}/restore` | Restore a previously soft-deleted user. | `users.delete` |
| `GET /admin/roles` | Paged roles with permission summaries. | `roles.read` |
| `POST /admin/roles` | Create role definition + permissions. | `roles.manage` |
| `PUT /admin/roles/{id}` | Update role metadata/permissions. | `roles.manage` |
| `DELETE /admin/roles/{id}` | Delete non-system role (when unused). | `roles.manage` |
| `GET /admin/permissions` | List canonical permissions from the catalog. | `roles.read` |

### Services & Helpers
- Authorization helpers (`RequireAdminPermission`) integrate with RBAC to enforce permission claims.
- Diagnostics options provide slow query logging and instrumentation hooks.
- DTOs (`AdminUserDetail`, `AdminRoleDetail`, etc.) shape responses for admin client consumption.

## Extension Points

- Provide custom `IAuthorizationHandler` implementations if you need to augment the default permission checks beyond `PermissionAuthorizationHandler`.
- Adjust admin request diagnostics via `AdminDiagnosticsOptions` (thresholds, logging behaviour).
- Replace email delivery or invite templates by overriding the services supplied by Identity Base (e.g., `ITemplatedEmailSender`).
- Map additional admin endpoints onto the `/admin` route group using the same authorization helpers.

## Dependencies & Compatibility

- Requires `Identity.Base` and `Identity.Base.Roles`.
- Currently scopes to system-wide administration; organization-aware admin tooling is a future extension.
- Compatible with `Identity.Base.Organizations` (admins can still call org endpoints; permissions control access).

## Examples & Guides

- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Getting Started Guide section on enabling admin endpoints.

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Admin` entries)
