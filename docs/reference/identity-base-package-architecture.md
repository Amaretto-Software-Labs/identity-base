# Identity Base Package Architecture

This note captures how the major Identity Base OSS packages relate to one another, how tenants/organizations/users/roles compose, and where the current gaps remain (notably admin surfacing for organizations).

## Package Overview

| Package | Purpose | Key Dependencies | Optional? | Activation |
| --- | --- | --- | --- | --- |
| `Identity.Base` | Core identity server: ASP.NET Identity, OpenIddict endpoints, MFA, profile flows. | — | Required foundation | Call `services.AddIdentityBase(...)` and map `app.MapApiEndpoints()` / Identity pipelines |
| `Identity.Base.Roles` | Role-based permission system (catalog, seeding, claim augmentation). | `Identity.Base` | Optional | `services.AddIdentityRoles(...)`, then `SeedIdentityRolesAsync()` and map role endpoints if desired |
| `Identity.Base.Organizations` | Organization domain + EF migrations, services, hosted seed/migration jobs, Minimal APIs, organization-aware claim formatter & scope resolver. | `Identity.Base`, `Identity.Base.Roles` | Optional | `services.AddIdentityBaseOrganizations(...)` and `app.MapIdentityBaseOrganizationEndpoints()` |
| `Identity.Base.Admin` | Opinionated admin endpoints for users/roles, admin authorization helpers. | `Identity.Base`, `Identity.Base.Roles` | Optional | `services.AddIdentityAdmin(...)` and `app.MapIdentityAdminEndpoints()` |

Packages compose in layers: you can run only the core, add RBAC when you need permissions, layer organizations for multi-org apps, and opt into admin endpoints last. Nothing turns on automatically—registration + endpoint mapping are explicit.

## Domain Relationships

- **Tenants** – Optional identifier on every organization/membership/role row. OSS treats `TenantId` as nullable; the commercial add-on will supply a tenant context and override the scope resolver. A tenant can own many organizations.
- **Organizations** – `Organization` entities group users under an optional tenant. They own `Memberships` and `Roles`, enforce unique `Slug` / `DisplayName` per tenant, and carry metadata.
- **Users** – Stored in the core Identity tables. Belong to organizations via `OrganizationMembership` records (composite key `{OrganizationId, UserId}`) that also track `IsPrimary` and link to an optional tenant.
- **Organization Roles** – `OrganizationRole` rows define org-scoped role names/descriptions. They can be global (no `OrganizationId`) or tied to a single org (and optionally a tenant). `OrganizationRoleAssignment` connects a role to a membership, while `OrganizationRolePermission` captures both default and per-organization permission overrides.
- **Permissions** – Defined in the RBAC package. `CompositePermissionResolver` in `Identity.Base.Roles` now unions role-based permissions with any registered `IAdditionalPermissionSource` implementations; `Identity.Base.Organizations` contributes one that surfaces the explicit entries stored in `Identity_OrganizationRolePermissions`.

## Runtime Integration

1. **Authentication** – OpenIddict tokens/cookies come from `Identity.Base`. When the organization package is registered, the default `OrganizationClaimFormatter` emits `org:id`, `org:slug`, and `org:name` claims whenever an active organization context is set. Clients that switch organizations must refresh tokens/cookies to pick up new claims.
2. **Authorization** – `OrganizationPermissionRequirement` delegates to the composite permission resolver. Minimal APIs call `.RequireOrganizationPermission("...")`; the default `OrganizationScopeResolver` enforces that the current user has a membership in the target organization. Apps can override the resolver (e.g., to allow tenant-wide admins) via `AddOrganizationScopeResolver<T>()` and add additional permission sources when emitting organization-specific claims.
3. **User Flows** – New endpoints (`GET /users/me/organizations`, `POST /users/me/organizations/active`) let a signed-in user list memberships and mark an active organization, wiring that into the context accessor.
4. **Admin Flows** – The admin package currently exposes `/admin/users` and `/admin/roles` only. There is **no** organization-aware admin surface yet; administrators either call the organization APIs directly (with the appropriate permissions) or we must extend `Identity.Base.Admin` with additional endpoints.

## Current Gaps / Follow-Up

- Org-specific admin endpoints (e.g., `/admin/organizations`) are not implemented. Further work should extend `Identity.Base.Admin` once requirements are clear.
- Organization roles now persist explicit permission overrides, but hosts must decide which permissions each custom role should receive by default (configure via `OrganizationRoleOptions` seed callbacks).
- Integration tests exercising Minimal APIs + authorization remain on the backlog; the current coverage focuses on service-level unit tests.

Use this architecture snapshot when deciding which packages to include in a host, how to configure token refresh after organization switches, and where to extend the admin surface.
