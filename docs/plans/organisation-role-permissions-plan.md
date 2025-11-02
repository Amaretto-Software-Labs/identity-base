# Plan: Configurable Organisation Roles & Permission Aggregation

## Context

Organisation-scoped roles currently suffer from two limitations:

1. **Hard-coded defaults** – the authorization handler only understands the seeded `OrgOwner`, `OrgManager`, `OrgMember` roles and their baked-in permissions.
2. **No unified permission view** – custom organisation roles do not contribute permissions, `/users/me/permissions` only exposes RBAC claims, and tokens/cookies ignore org-scoped access entirely.

As a result, new organisation roles are “decorative” unless callers also grant equivalent global roles, and hosts cannot adjust the default org role set without modifying code.

## Goals

- Allow hosts to define organisation roles (names, descriptions, default permissions) via configuration or seed callbacks without modifying library code.
- Persist per-organisation permission mappings so each org can tailor its role behavior.
- Merge organisation-scoped permissions with RBAC permissions for authorization checks, `/users/me/permissions`, and emitted identity tickets.
- Keep behavior intact for hosts that do not reference `Identity.Base.Organisations`.

## Non-Goals

- Replacing or extending the admin API with organisation management endpoints (tracked separately).
- Implementing UI/UX beyond updating existing sample/admin clients needed to exercise the new APIs.
- Introducing multi-tenant context enforcement beyond what already exists.

## Deliverables

1. EF schema/migration updates.
2. Service and authorization pipeline changes.
3. Aggregated permissions endpoint/token updates.
4. New/updated APIs for managing org role permissions and configurable defaults.
5. Documentation + tests covering the new behavior.

## Priority Breakdown

### Priority 1 — Core Infrastructure

1. **Schema & configuration**
   - Add `Identity_OrganisationRolePermissions` table linking `OrganisationRole` → RBAC permission IDs with optional `TenantId`/`OrganisationId`.
   - Extend `OrganisationRoleOptions` (or introduce a dedicated options object) to define seedable default org roles + permission bundles.
   - Update seeders to populate defaults only when tables are empty.

2. **Permission resolver**
   - Implement `IOrganisationPermissionResolver` to union RBAC permissions with org-scoped permissions for a `(userId, organisationId)` pair.
   - Remove hard-coded role maps from `OrganisationPermissionAuthorizationHandler`, delegating entirely to the resolver.

3. **Migrating existing data**
   - Backfill permission mappings for the current defaults so existing installs retain behavior after the migration.
   - Provide upgrade notes describing manual steps if hosts renamed the default roles.

### Priority 2 — Unified Permission Surface

1. **Permission aggregation contract**
   - Add `IAdditionalPermissionSource` (name TBD) in `Identity.Base.Roles` with a default no-op implementation.
   - Modify `/users/me/permissions` to merge RBAC permissions with all registered sources.
   - Ensure identity ticket generation (JWT/cookies) uses the same aggregation path.

2. **Organisation provider**
   - Register an implementation in the organisations package that returns org-scoped permissions when an active org context exists.
   - Handle the “no organisation selected” case by returning an empty set.

### Priority 3 — Management APIs & Tooling

1. **Org role permission CRUD**
   - Extend existing org role endpoints or add dedicated routes (e.g., `PUT /organisations/{id}/roles/{roleId}/permissions`) to manage the permission list.
   - Update the React org add-on + sample client to surface permission selection per role.
   - Document the API shape and sample usage.

2. **Configurable defaults**
   - Document how to define default org roles/permissions via configuration or custom seeding.
   - Provide helpers for hosts to replace or remove the initial default set.

### Priority 4 — Validation & Docs

1. **Tests**
   - Unit tests for the resolver/handler and aggregation interface (org-enabled vs. org-disabled hosts).
   - Integration tests validating `/users/me/permissions` and authorization decisions before/after switching organisations.
   - Migration tests ensuring backfilled defaults apply cleanly.

2. **Documentation**
   - Update existing guides (`organisation-admin-use-case`, `rbac-design`, `package architecture`) with new behavior.
   - Add upgrade notes detailing schema changes, seeding expectations, and rollout steps.

## Decisions & Open Topics

- **Org role mapping strategy** – Organisation roles will store explicit permission lists (referencing RBAC permission IDs). This avoids tight coupling to global role names, keeps lookups deterministic, and lets organisations diverge from tenant-wide roles without cross-contaminating assignments. Hosts can still reuse global definitions by copying the same permission sets if desired.
- **Permission aggregation semantics** – Additive union is sufficient for v1; no deny/override semantics will be implemented.
- **Admin/tooling placement** – Revisit after implementation. Initial work can live in samples; we will refactor into shared packages once the API surface stabilizes.

## Next Steps

1. Kick off schema work (Priority 1) with detailed migration & options design.
2. Align on API surface for managing role permissions before updating clients.
3. Schedule documentation/upgrade note updates alongside testing closure.

## Implementation Status (2025-10-27)

- ✅ Priority 1 – Schema, options model, seeding and resolver infrastructure are in place. Existing installs receive backfilled permission rows.
- ✅ Priority 2 – `CompositePermissionResolver` aggregates RBAC + organisation sources; tokens/endpoints now surface the union.
- ✅ Priority 3 – REST endpoints and React tooling expose per-role permission editing; sample client demonstrates the flow.
- ✅ Priority 4 – Additional unit tests cover permission resolution/update paths and documentation reflects the new behavior.
- ⏳ Priority 5 – Large-scale integration/perf tests remain on the backlog and will be scheduled separately.
