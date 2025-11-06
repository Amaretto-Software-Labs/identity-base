# Use Case: Multi-Organization SaaS with Organization Administrators

This guide captures the end-to-end scenario described in planning: users register, create or join organizations, organization administrators manage memberships/roles, and a separate admin surface can oversee the entire system.

> ✅ Sample implementation: `apps/org-sample-api` + `apps/org-sample-client` implement registration, organization creation, invites, and member management end-to-end. See also [docs/guides/organization-onboarding-flow.md](organization-onboarding-flow.md) for a step-by-step walkthrough.

## Scenario Snapshot

- **Registration**: A new user signs up, names a new organization, and is automatically granted an administrator role for that organization.
- **Organization Admins**: Can invite users, assign organization roles, create/edit/delete custom org roles, and remove members.
- **Users**: May belong to several organizations, hold different roles per organization, and always retain self-service profile/MFA/password flows from Identity Base.
- **Global Admins**: Use a dedicated admin application to manage every user and (future) organization across tenants.

## Package Checklist

For detailed documentation on each package referenced below, use the [Package Documentation Hub](../packages/README.md).

| Layer | Package | Required? | Notes |
| --- | --- | --- | --- |
| Core Identity | `Identity.Base` | ✅ | Provides registration/login, MFA, profile APIs, OpenIddict integration. |
| RBAC | `Identity.Base.Roles` | ✅ | Supplies permission catalog, role seeding, claim formatter used by both the public APIs and admin surfaces. |
| Organizations | `Identity.Base.Organizations` | ✅ | Delivers organization domain + CRUD/Membership/Role APIs, organization claim formatter, scope enforcement. |
| Admin APIs | `Identity.Base.Admin` | ✅ (for back-office) | Ships `/admin/users` & `/admin/roles`; organization admin endpoints (`/admin/organizations/...`) come from the organizations package. |

> Skip any optional layer only if the corresponding capability is not needed. The organization scenario assumes all four are present.

## Implementation Checklist

- [ ] **Wire Identity Base**
  - `services.AddIdentityBase(configuration, environment)` and `app.MapApiEndpoints()`.
  - Ensure OpenIddict clients/apps that will consume organization context can refresh tokens (e.g., using refresh tokens or cookie re-issue).

- [ ] **Enable RBAC**
  - `var rolesBuilder = services.AddIdentityRoles(configuration);`
  - Choose storage (`rolesBuilder.AddDbContext<IdentityRolesDbContext>(...)`).
  - Seed definitions on startup via `await app.Services.SeedIdentityRolesAsync();`.
  - Define permissions covering organization management (e.g. `admin.organizations.manage`, `admin.organizations.members.manage`, with matching `user.organizations.*` entries seeded for future scoped endpoints).

- [ ] **Add Organizations Package**
  - `var orgBuilder = services.AddIdentityBaseOrganizations(options => options.UseNpgsql(...));`
  - Map endpoints with `app.MapIdentityBaseOrganizationEndpoints();`.
  - Configure metadata as needed (`ConfigureOrganizationModel`, `AfterOrganizationSeed`, custom scope resolver/claim formatter overrides).
  - Decide how the default org roles (`OrgOwner`, `OrgManager`, `OrgMember`) map to RBAC permissions.

- [ ] **Registration Flow Enhancements**
  - Extend the post-registration pipeline to call `IOrganizationService.CreateAsync(...)` and create a membership via `IOrganizationMembershipService.AddMemberAsync(...)`.
  - Assign the creator the `OrgOwner` (organization admin) role. Optionally mark the membership as `IsPrimary` for default context.
  - Trigger token/cookie refresh so the new organization context appears in claims.

- [ ] **Invitation & Membership Management**
  - Use admin invitation endpoints:
    - `POST /admin/organizations/{orgId}/invitations` to issue an invite (backed by `OrganizationInvitationService` which stores the record, enforces uniqueness, and applies expiry).
    - `GET /admin/organizations/{orgId}/invitations` / `DELETE .../{code}` to list or revoke invites.
    - Public flow: `GET /invitations/{code}` for validation and `POST /invitations/claim` (authenticated) to accept; the service automatically creates/updates the membership and returns `RequiresTokenRefresh = true`.
  - Use admin membership endpoints when you already know the user id:
    - `POST /admin/organizations/{orgId}/members` to add a member immediately.
    - `PUT /admin/organizations/{orgId}/members/{userId}` to update role assignments / primary flag.
    - `DELETE /admin/organizations/{orgId}/members/{userId}` to remove members.
  - Hosts are still responsible for the invite delivery UX (email templates, SPA acceptance page) even though storage and APIs are provided.

- [ ] **Organization Role Management**
  - Organization admins call `GET/POST/DELETE /admin/organizations/{orgId}/roles` for custom roles.
  - `GET /admin/organizations/{orgId}/roles/{roleId}/permissions` returns inherited vs. explicit permission assignments; `PUT` replaces the explicit list stored in `Identity_OrganizationRolePermissions`.
  - Default permission bundles can be configured via `OrganizationRoleOptions.DefaultRoles`; per-organization overrides flow through the same service layer and are surfaced in tokens/claims.

- [ ] **User Self-Service**
  - Existing Identity Base endpoints handle profile updates, password changes, MFA enable/disable. No extra work needed beyond exposing the routes in the client application.

- [ ] **Organization Context Handling**
  - Call `GET /users/me/organizations` to show available orgs.
  - Add `app.UseOrganizationContextFromHeader()` and send the `X-Organization-Id` header so each request carries the active organization without reissuing tokens. There is no backend endpoint to set the active org; the header alone establishes context. Refresh tokens only when membership changes (e.g., an owner loses access).

- [ ] **Admin Application**
  - Use `Identity.Base.Admin` for user & role management (`services.AddIdentityAdmin(...)`, `app.MapIdentityAdminEndpoints()`).
  - Organization CRUD, membership, role, and invitation management lives under `/admin/organizations/...` (mapped by `app.MapIdentityBaseOrganizationEndpoints()`). The admin SPA should call these endpoints with the appropriate `admin.organizations.*` permissions; no organization header is required for these calls.

## Configuration Notes

- **Authorization**: Organizations endpoints enforce permissions via `RequireOrganizationPermission("...")` and rely on `OrganizationScopeResolver` to ensure the acting user belongs to the target organization. Override the resolver when building tenant-aware or super-admin flows.
- **Claims**: Tokens/cookies only include organization info after the context accessor has a value. Ensure your client orchestrates a refresh post-switch.
- **Tenant Support**: `TenantId` columns are optional in OSS. If multi-tenant behavior is required, supply a custom scope resolver/claim formatter that includes tenant context and enforces tenant-level constraints.

## Known Gaps / Required Extensions

| Area | Gap | Suggested Action |
| --- | --- | --- |
| Invitations UX | Email templates, delivery, and the public-facing acceptance page remain host responsibilities (the invitation APIs handle persistence and membership updates). | Implement a lightweight mailer + SPA flow that calls `/admin/organizations/{id}/invitations`, `/invitations/{code}`, and `/invitations/claim`. |
| Org ↔ RBAC binding | Explicit permission overrides are persisted, but hosts must still decide which permissions correspond to new org roles. | Define default permission bundles in configuration/seed callbacks and expose admin UX (see sample) for fine-grained overrides. |
| Admin UI | Admin SPA must target `/admin/organizations/...` endpoints for org oversight. | Ensure admin packages/clients are updated to new routes and guard them with the `admin.organizations.*` permissions. |
| Integration Tests | Minimal APIs currently validated by unit tests only. | Add WebApplicationFactory-based tests to cover authorization and org-switch flows end-to-end. |

Use this checklist as a blueprint when standing up a new multi-organization application. Each unchecked gap represents work the host application (or a future OSS contribution) must address before the scenario is fully supported.
