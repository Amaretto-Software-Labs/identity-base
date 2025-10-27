# Admin Operations Guide

This guide walks operators through enabling and using the Identity Base admin surface. It covers API configuration, required roles and permissions, and the day-to-day workflows available in the sample React console (`apps/sample-client`).

> Repository: [Identity Base on GitHub](https://github.com/Amaretto-Software-Labs/identity-base)  
> Documentation index: [`docs/`](https://github.com/Amaretto-Software-Labs/identity-base/tree/main/docs)  
> Issues & support: [GitHub Issues](https://github.com/Amaretto-Software-Labs/identity-base/issues)

## 1. Prerequisites
- Identity Base host running (e.g., `dotnet run --project Identity.Base.Host`).
- Database schema up to date. The host automatically applies the Identity and Roles migrations via background hosted services on startup.
- At least one admin user seeded with the appropriate roles/permissions (`users.read`, `users.manage-roles`, `roles.read`, `roles.manage`, `users.create`, etc.). You can configure these in `Roles:Definitions` and `Roles:DefaultAdminRoles` inside `appsettings`.
- OAuth client with the `identity.admin` scope. The sample SPA requests this scope by default.

## 2. Configuration Checklist
1. **Roles & Permissions**
   - Define admin-facing permissions under `Permissions:Definitions` in configuration.
   - Verify `Roles:Definitions` maps each role to the required permission set (e.g., `identity.admin` role includes `users.*` and `roles.*`).
   - Set `Roles:DefaultAdminRoles` to assign the admin role automatically when you seed or create an administrator.

2. **Seeding Admin Accounts**
   - Use `IdentitySeed` options to create a bootstrap admin account on startup, or register an account manually and assign roles via `/admin/users/{id}/roles` once you have an initial admin.

3. **Scopes & Clients**
   - Configure the admin OAuth client in `OpenIddict:Applications` with the `identity.admin` scope.
   - Update `apps/sample-client/.env` (or rely on defaults) to request that scope: `VITE_AUTHORIZE_SCOPE="openid profile email offline_access identity.api identity.admin"`.

## 3. Starting the Admin Console
1. Run the host: `dotnet run --project Identity.Base.Host` (listens on `https://localhost:5000` by default).
2. Launch the sample client: `cd apps/sample-client && npm install && npm run dev` (hosts at `http://localhost:5173`).
3. Sign in with an administrator account. Once authenticated, the navigation bar exposes an **Admin** link.
4. The admin route is protected by a permission guard that calls `/users/me/permissions`. The link and pages load only if the current user holds at least `users.read` and `roles.read`.

## 4. Key Workflows
### 4.1 User Management (`/admin/users`)
- **List & Search:** `GET /admin/users` accepts `page`, `pageSize` (max 100), `search`, `role`, `locked`, `deleted`, and `sort` (`createdAt[:asc|:desc]`, `email[:asc|:desc]`). Combine these to page through large directories without downloading every user.
- **Create Users:** optional display name, role assignments, confirmation/password reset emails.
- **Mutations:** lock/unlock, soft delete/restore, force password reset, reset MFA, resend confirmation email.
- **Detail View:** update profile flags, phone attributes, metadata; inspect external logins; manage role assignments inline.
- **Audit Trail:** admin actions emit audit events (e.g., `AdminUserLocked`, `AdminUserRolesUpdated`). Check your configured audit sink for details.

### 4.2 Role Management (`/admin/roles`)
- **CRUD:** create, edit, or delete roles (system roles cannot be deleted or renamed).
- **List Options:** `GET /admin/roles` supports the same `page`/`pageSize` pattern plus `search`, `isSystemRole`, and `sort` (`name[:desc]`, `userCount[:asc|:desc]`).
- **Permissions:** select from the catalog defined in configuration; custom permissions can be added directly in the UI. `GET /admin/permissions` mirrors the paging API (`page`, `pageSize`, `search`, `sort=name|roleCount`) for large catalogs.
- **Usage Count:** each role entry shows how many users are assigned, preventing accidental removal of in-use roles.

## 5. API Reference Snapshot
- `GET /admin/users` — list users (requires `users.read`).
- `GET /admin/users/{id}` — fetch user detail (`users.read`).
- `POST /admin/users` — create user (`users.create`).
- `PUT /admin/users/{id}` — update profile flags/metadata (`users.update`).
- `POST /admin/users/{id}/lock` / `unlock` — mutate lockout state (`users.lock`).
- `POST /admin/users/{id}/force-password-reset` — send reset email (`users.reset-password`).
- `POST /admin/users/{id}/mfa/reset` — clear MFA enrollment (`users.reset-mfa`).
- `PUT /admin/users/{id}/roles` — replace role assignments (`users.manage-roles`).
- `DELETE /admin/users/{id}` — soft delete (`users.delete`).
- `POST /admin/users/{id}/restore` — restore (`users.delete`).
- `GET /admin/roles` — list roles (`roles.read`).
- `POST /admin/roles` — create role (`roles.manage`).
- `PUT /admin/roles/{id}` — update role (`roles.manage`).
- `DELETE /admin/roles/{id}` — delete role (`roles.manage`).
- `GET /admin/permissions` — list permission catalog (`roles.read`).
- `GET /users/me/permissions` — resolves effective permissions for the signed-in user (returns an array of strings).

## 6. Troubleshooting
- **404 on `/users/me/permissions`:** ensure `app.MapIdentityRolesUserEndpoints()` is called (already wired in `Identity.Base.Host/Program.cs`).
- **Admin link missing:** verify the signed-in user has `roles.read` and `users.read`. Also confirm `/users/me/permissions` is returning data (watch the network tab or use curl).
- **403 responses:** double-check that the access token or cookie contains both the admin scope and the required permission claims.
- **Migrations failing:** the host applies migrations on startup via hosted services; inspect logs for `Failed to apply role database migrations` errors. Fix connection strings before restarting.

## 7. Next Steps
- Customize the React admin pages by copying `apps/sample-client/src/pages/admin` into your own SPA and replacing the styling or UX components.
- Extend the permission catalog with domain-specific actions. Ensure configuration and UI selectors stay in sync.
- Integrate audit events with your observability stack so each privileged action is tracked end-to-end.

For broader setup guidance (mail, OAuth clients, Docker compose), refer to `docs/guides/getting-started.md` and `docs/guides/integration-guide.md`.

## Resources
- [Identity Base repository](https://github.com/Amaretto-Software-Labs/identity-base)
- [Documentation folder](https://github.com/Amaretto-Software-Labs/identity-base/tree/main/docs)
- [Issue tracker](https://github.com/Amaretto-Software-Labs/identity-base/issues)
- [AGENTS guide](../../AGENTS.md) — contributor playbook and non-negotiable rules
