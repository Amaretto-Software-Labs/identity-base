# Identity.Base.Admin

## Overview
`Identity.Base.Admin` adds system-administrator endpoints on top of Identity Base + RBAC. It surfaces `/admin/users`, `/admin/roles`, and `/admin/permissions` Minimal APIs, enforces admin-specific permission requirements (e.g., `users.read`, `roles.manage`), and plugs into the shared permission resolver so token claims remain consistent across admin and user experiences.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Admin
```

Register the admin services after `AddIdentityBase`:

```csharp
using Identity.Base.Admin;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Data;
using Microsoft.EntityFrameworkCore;

var rolesBuilder = builder.Services.AddIdentityAdmin(builder.Configuration); // internally calls AddIdentityRoles
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var app = builder.Build();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints(); // /admin/users, /admin/roles, /admin/permissions
await app.Services.SeedIdentityRolesAsync();
```

`AddIdentityAdmin` configures admin authorization helpers (`PermissionAuthorizationHandler` + `RequireAdminPermission`) and returns the underlying `IdentityRolesBuilder` so you can continue configuring RBAC storage.

## Configuration

Two option sections control behaviour:

| Section | Purpose | Default |
| --- | --- | --- |
| `IdentityAdmin` (`AdminApiOptions`) | Required OAuth scope for admin endpoints (`RequiredScope`). Set to `null` to skip scope checks. | `identity.admin` |
| `Identity:Admin:Diagnostics` (`AdminDiagnosticsOptions`) | Logging/metrics thresholds (`SlowQueryThreshold`). | 500 ms |

Grant the admin scope to trusted clients (e.g., management SPA) via `OpenIddict:Applications` and assign the admin roles (`roles.manage`, `users.manage-roles`, etc.) so the permission resolver can authorise requests.

## Public Surface

### Minimal API Endpoints

| Route | Description | Required Permission |
| --- | --- | --- |
| `GET /admin/users` | Paged list of users with filtering/sorting (supports `page`, `pageSize`, `search`, `role`, `locked`, `deleted`, `sort`). | `users.read` |
| `GET /admin/users/{id}` | Detailed user profile including MFA state, external logins, metadata. | `users.read` |
| `POST /admin/users` | Create a user. Request supports assigning roles, metadata, and optionally sending confirmation/reset emails. | `users.create` |
| `PUT /admin/users/{id}` | Update profile metadata, email confirmation flag, lockout settings. | `users.update` |
| `POST /admin/users/{id}/lock` / `unlock` | Force lockout or clear lockout. | `users.lock` |
| `POST /admin/users/{id}/force-password-reset` | Generates a reset token and sends the email via `IAccountEmailService`. | `users.reset-password` |
| `POST /admin/users/{id}/mfa/reset` | Disables MFA and resets the authenticator key. | `users.reset-mfa` |
| `POST /admin/users/{id}/resend-confirmation` | Sends another confirmation email if the user is unconfirmed. | `users.update` |
| `GET /admin/users/{id}/roles` | Returns roles assigned to the user. | `users.manage-roles` |
| `PUT /admin/users/{id}/roles` | Replace the user's role list. | `users.manage-roles` |
| `DELETE /admin/users/{id}` / `POST /admin/users/{id}/restore` | Soft delete and restore accounts. | `users.delete` |
| `GET /admin/roles` | Paged list of roles including permission summaries and user counts. Supports search/sort filters. | `roles.read` |
| `POST /admin/roles` | Create a role with explicit permissions. | `roles.manage` |
| `PUT /admin/roles/{id}` | Update name/description/permissions of a role. | `roles.manage` |
| `DELETE /admin/roles/{id}` | Delete a role (fails if system role or in use). | `roles.manage` |
| `GET /admin/permissions` | List canonical permissions with usage counts. Supports `page`, `pageSize`, `search`, `sort`. | `roles.read` |

Example request:

```bash
curl -X GET https://identity.example.com/admin/users?page=1&pageSize=20 \
     -H "Authorization: Bearer $ADMIN_ACCESS_TOKEN"

# Response (truncated)
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 123,
  "users": [
    {
      "id": "...",
      "email": "alice@example.com",
      "displayName": "Alice",
      "emailConfirmed": true,
      "isLockedOut": false,
      "mfaEnabled": true,
      "roles": ["IdentityAdmin"],
      "isDeleted": false
    }
  ]
}
```

### Services & Helpers
- `RequireAdminPermission` – extension on `AuthorizationPolicyBuilder` that checks for the specified permission claim (`identity.permissions`).
- `PermissionAuthorizationHandler` – central handler used by all admin endpoints; enforces both scope (if configured) and permission requirements.
- Diagnostics metrics (`AdminMetrics`) – emitted when Admin endpoints execute; slow queries trigger warnings based on `AdminDiagnosticsOptions`.

## Dependencies & Compatibility
- Requires `Identity.Base` and `Identity.Base.Roles`.
- Works alongside `Identity.Base.Organizations`; admin endpoints operate on all organisations regardless of the `X-Organization-Id` header (the header is ignored intentionally for admin routes).
- Shares `IdentityRolesDbContext`; no additional migrations beyond the RBAC package are needed.

## Troubleshooting & Tips
- **403 Forbidden** – confirm the bearer token contains both the admin scope (if `RequiredScope` is set) and the necessary permission (e.g., `users.read`). The `/users/me/permissions` endpoint from the Roles package can help debug.
- **Slow admin queries** – adjust `Identity:Admin:Diagnostics:SlowQueryThreshold` or investigate database indexing (e.g., add indexes on `ApplicationUser` via `ConfigureAppDbContextModel`).
- **Emails not sending** – admin endpoints defer to the same `IAccountEmailService` used by Identity Base. Ensure the Mailjet (or custom) sender is wired correctly.
- **Role deletion conflicts** – deleting a role that is assigned to users returns `409 Conflict`. Remove assignments via `PUT /admin/users/{id}/roles` first.

## Examples & Guides
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- [Admin Operations Guide](../../guides/admin-operations-guide.md)
- Sample admin wiring: `Identity.Base.Host/Program.cs`
 - Playbook: ../../playbooks/admin-api-smoke.md

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Admin` entries)
