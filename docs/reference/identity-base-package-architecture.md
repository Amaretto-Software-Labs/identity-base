# Identity Base Package Architecture

This note captures how the major Identity Base OSS packages relate to one another, how tenants/organisations/users/roles compose, and where the current gaps remain (notably admin surfacing for organisations).

## Package Overview

| Package | Purpose | Key Dependencies | Optional? | Activation |
| --- | --- | --- | --- | --- |
| `Identity.Base` | Core identity server: ASP.NET Identity, OpenIddict endpoints, MFA, profile flows. | — | Required foundation | Call `services.AddIdentityBase(...)` and map `app.MapApiEndpoints()` / Identity pipelines |
| `Identity.Base.Roles` | Role-based permission system (catalog, seeding, claim augmentation). | `Identity.Base` | Optional | `services.AddIdentityRoles(...)`, then `SeedIdentityRolesAsync()` and map role endpoints if desired |
| `Identity.Base.Organisations` | Organisation domain + EF migrations, services (memberships, roles, invitations), hosted seed/migration jobs, Minimal APIs, organisation-aware claim formatter & scope resolver. | `Identity.Base`, `Identity.Base.Roles` | Optional | `services.AddIdentityBaseOrganisations(...)` and `app.MapIdentityBaseOrganisationEndpoints()` |
| `Identity.Base.Admin` | Opinionated admin endpoints for users/roles, admin authorization helpers. | `Identity.Base`, `Identity.Base.Roles` | Optional | `services.AddIdentityAdmin(...)` and `app.MapIdentityAdminEndpoints()` |

Packages compose in layers: you can run only the core, add RBAC when you need permissions, layer organisations for multi-org apps, and opt into admin endpoints last. Nothing turns on automatically—registration + endpoint mapping are explicit.

## Domain Relationships

- **Tenants** – Optional identifier on every organisation/membership/role row. OSS treats `TenantId` as nullable; the commercial add-on will supply a tenant context and override the scope resolver. A tenant can own many organisations.
- **Organisations** – `Organisation` entities group users under an optional tenant. They own `Memberships` and `Roles`, enforce unique `Slug` / `DisplayName` per tenant, and carry metadata.
- **Users** – Stored in the core Identity tables. Belong to organisations via `OrganisationMembership` records (composite key `{OrganisationId, UserId}`) that also track `IsPrimary` and link to an optional tenant.
- **Organisation Roles** – `OrganisationRole` rows define org-scoped role names/descriptions. They can be global (no `OrganisationId`) or tied to a single org (and optionally a tenant). `OrganisationRoleAssignment` connects a role to a membership, while `OrganisationRolePermission` captures both default and per-organisation permission overrides.
- **Permissions** – Defined in the RBAC package. `CompositePermissionResolver` in `Identity.Base.Roles` now unions role-based permissions with any registered `IAdditionalPermissionSource` implementations; `Identity.Base.Organisations` contributes one that surfaces the explicit entries stored in `Identity_OrganisationRolePermissions`.

## Runtime Integration

1. **Authentication** – OpenIddict tokens/cookies come from `Identity.Base`. When the organisation package is registered, the default `OrganisationClaimFormatter` emits `org:id`, `org:slug`, and `org:name` claims whenever an active organisation context is set. Clients that switch organisations must refresh tokens/cookies to pick up new claims.
2. **Authorization** – `OrganisationPermissionRequirement` delegates to the composite permission resolver. Minimal APIs call `.RequireOrganisationPermission("...")`; the default `OrganisationScopeResolver` enforces that the current user has a membership in the target organisation. Apps can override the resolver (e.g., to allow tenant-wide admins) via `AddOrganisationScopeResolver<T>()` and add additional permission sources when emitting organisation-specific claims.
3. **User Flows** – New endpoints (`GET /users/me/organisations`, `POST /users/me/organisations/active`) let a signed-in user list memberships and mark an active organisation, wiring that into the context accessor.
4. **Invitations** – Shared invitation endpoints (`/organisations/{id}/invitations`, `/invitations/{code}`, `/invitations/claim`) now ship with the organisations package. Hosts can use the default Minimal APIs or call `OrganisationInvitationService` directly when composing custom onboarding flows.
5. **Admin Flows** – The admin package currently exposes `/admin/users` and `/admin/roles` only. There is **no** organisation-aware admin surface yet; administrators either call the organisation APIs directly (with the appropriate permissions) or we must extend `Identity.Base.Admin` with additional endpoints.

## Current Gaps / Follow-Up

- Org-specific admin endpoints (e.g., `/admin/organisations`) are not implemented. Further work should extend `Identity.Base.Admin` once requirements are clear.
- Organisation roles now persist explicit permission overrides, but hosts must decide which permissions each custom role should receive by default (configure via `OrganisationRoleOptions` seed callbacks).
- Integration tests exercising Minimal APIs + authorization remain on the backlog; the current coverage focuses on service-level unit tests.

Use this architecture snapshot when deciding which packages to include in a host, how to configure token refresh after organisation switches, and where to extend the admin surface.
