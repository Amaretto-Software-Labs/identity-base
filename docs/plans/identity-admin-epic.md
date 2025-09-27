# Identity Base Admin Epic Plan

## Overview
Deliver comprehensive administrative capabilities while keeping RBAC reusable for consumers who don’t need the full admin surface. Introduce two new packages (see `docs/reference/rbac-design.md` for the foundational model):
- **`Identity.Base.Roles`** – core RBAC primitives (roles, permissions, defaults, user-role assignment, `/users/me/permissions`).
- **`Identity.Base.Admin`** – privileged admin APIs, UI integration helpers, audit logging extensions (depends on `Identity.Base.Roles`).

Extend the sample React client with admin flows and document the configuration, testing, and rollout steps.

---

## 1. Requirements & Authorization Model
- **Roles & permissions:** model named roles as collections of granular permissions (e.g., `users.read`, `users.update`, `users.lock`, etc.). Users may hold multiple roles; effective permissions are the union of role permissions. No direct per-user permission grants.
- **Admin access:** expose a separate admin OAuth client/scope (e.g., `identity.admin`). Admin endpoints enforce both the admin scope and the specific permission conveyed by the admin’s roles.
- **Audit logging:** persist all admin actions—and optionally all user-facing actions—to an audit trail table (configurable via `Audit:Enabled`, default true).

---

## 2. Package Responsibilities

### Identity.Base.Roles
- Role/permission entities, configuration binding, and seeding.
- Default role assignment on user creation (self-registration & admin-created).
- Services for resolving effective permissions (union of roles).
- `/users/me/permissions` endpoint for end users.
- Optional DI extensions to expose role information to consuming apps.

### Identity.Base.Admin (depends on Roles package)
- Admin endpoints for managing users, roles, MFA, audits.
- DI extensions for registering admin APIs.
- Additional services for admin operations (force reset, MFA reset, audit write).
- React client helpers/hooks for admin workflows.

---

## 3. Admin API Design (`Identity.Base.Admin`)

### User management (`/admin/users`)
| Endpoint | Description | Required Permission |
| --- | --- | --- |
| `GET /admin/users` | Paged/filterable list (email, status, roles, created, last activity) | `users.read` |
| `GET /admin/users/{id}` | Detailed profile (metadata, MFA status, external logins, audit summary) | `users.read` |
| `POST /admin/users` | Create user (with initial roles) | `users.create` |
| `PUT /admin/users/{id}` | Update profile metadata, flags (lockout, email confirmation, etc.) | `users.update` |
| `POST /admin/users/{id}/lock` | Lock account | `users.lock` |
| `POST /admin/users/{id}/unlock` | Unlock account | `users.lock` |
| `POST /admin/users/{id}/force-password-reset` | Generate/reset token & email user | `users.reset-password` |
| `POST /admin/users/{id}/mfa/reset` | Clear MFA enrollment | `users.reset-mfa` |
| `POST /admin/users/{id}/resend-confirmation` | Resend confirmation email | `users.update` |
| `GET /admin/users/{id}/roles` | View assigned roles | `users.manage-roles` |
| `PUT /admin/users/{id}/roles` | Assign/remove roles | `users.manage-roles` |
| `DELETE /admin/users/{id}` | Soft-delete user | `users.delete` |
| `POST /admin/users/{id}/restore` | Restore soft-deleted user | `users.delete` |

### Role management (`/admin/roles`)
| Endpoint | Description | Required Permission |
| --- | --- | --- |
| `GET /admin/roles` | List roles and permissions | `roles.read` |
| `POST /admin/roles` | Create new role (name + permissions) | `roles.manage` |
| `PUT /admin/roles/{id}` | Update role definition | `roles.manage` |
| `DELETE /admin/roles/{id}` | Delete role (guard against in-use roles) | `roles.manage` |

---

## 4. Implementation Outline

### Identity.Base.Roles
- New project with:
  - Role/permission entities + EF Core configurations.
  - Configuration binding: `Roles:Definitions`, `Roles:DefaultUserRoles`, `Roles:DefaultAdminRoles`.
  - Seeding logic to create default roles if absent.
  - Services for assigning roles, resolving permissions.
  - Minimal API endpoints (e.g., `GET /users/me/permissions`).
  - Unit tests for permission resolution & defaults.

### Identity.Base.Admin
- Depends on `Identity.Base.Roles`.
- Feature folders (`Features/AdminUsers`, `Features/AdminRoles`, etc.) with minimal APIs, DTOs, validators.
- Authorization policies combining scope + permission checks.
- Services for admin-specific actions (force password reset, audit logging, MFA reset).
- Integration tests for each endpoint (happy path, forbidden, validation, audits).

---

## 5. Client Integration (React)
- Extend sample React app with admin routes:
  - `/admin/users` list/search/filter.
  - `/admin/users/:id` detail view (roles, MFA, audit snippet).
  - `/admin/roles` role management (create/edit/delete, assign permissions).
- Update `@identity-base/react-client` with admin hooks (list users, assign roles, manage roles).
- Provide UI patterns (modals, confirm dialogs, toast notifications).
- Update React integration guide with instructions for enabling admin features.

---

## 6. Configuration & Deployment
- Appsettings:
  - `Roles:Definitions`, `Roles:DefaultUserRoles`, `Roles:DefaultAdminRoles`.
  - `Audit:Enabled` toggle.
  - Admin OAuth client/scope configuration.
  - CORS entries for admin SPA (if separate origin).
- MailJet templates for admin-triggered emails (forced resets) if needed.
- Seeding ensures default roles and at least one admin user per environment.

---

## 7. Testing Strategy
- **Roles package:** unit tests for role assignment, permission resolution, default seeding, and `GET /users/me/permissions` integration tests.
- **Admin package:** integration tests covering all endpoints (authorization, validation, audit logging).
- **React admin UI:** component/integration tests for critical flows.
- **Postman collection / scripts:** document admin workflows (list users, assign roles, reset MFA, soft delete).

---

## 8. Documentation
- Update `docs/reference/identity-base-public-api.md` with RBAC and admin endpoints.
- Author `docs/guides/admin-operations-guide.md` and update `docs/guides/react-integration-guide.md` with admin onboarding.
- Amend getting-started and release checklist to include role configuration and admin seeding steps.

---

## 9. Rollout
- Publish `Identity.Base.Roles` first (optional dependency).
- Publish `Identity.Base.Admin` (depends on Roles) with changelog and upgrade guidance.
- No feature flag needed; endpoints guarded by scope + permission checks.
- Gather feedback and iterate (e.g., bulk actions, tenant scoping in future epics).

---

## Deliverables Summary
1. `Identity.Base.Roles` package (role definitions, defaults, `/users/me/permissions`).
2. `Identity.Base.Admin` package (admin endpoints, audit integrations).
3. React admin UI updates and hooks.
4. Documentation updates (API reference, admin guide, release notes).
5. Comprehensive tests and Postman collection.
