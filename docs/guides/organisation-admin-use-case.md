# Use Case: Multi-Organisation SaaS with Organisation Administrators

This guide captures the end-to-end scenario described in planning: users register, create or join organisations, organisation administrators manage memberships/roles, and a separate admin surface can oversee the entire system.

> ✅ Sample implementation: `apps/org-sample-api` + `apps/org-sample-client` implement registration, organisation creation, invites, and member management end-to-end. See also [docs/guides/organisation-onboarding-flow.md](organisation-onboarding-flow.md) for a step-by-step walkthrough.

## Scenario Snapshot

- **Registration**: A new user signs up, names a new organisation, and is automatically granted an administrator role for that organisation.
- **Organisation Admins**: Can invite users, assign organisation roles, create/edit/delete custom org roles, and remove members.
- **Users**: May belong to several organisations, hold different roles per organisation, and always retain self-service profile/MFA/password flows from Identity Base.
- **Global Admins**: Use a dedicated admin application to manage every user and (future) organisation across tenants.

## Package Checklist

| Layer | Package | Required? | Notes |
| --- | --- | --- | --- |
| Core Identity | `Identity.Base` | ✅ | Provides registration/login, MFA, profile APIs, OpenIddict integration. |
| RBAC | `Identity.Base.Roles` | ✅ | Supplies permission catalog, role seeding, claim formatter used by both the public APIs and admin surfaces. |
| Organisations | `Identity.Base.Organisations` | ✅ | Delivers organisation domain + CRUD/Membership/Role APIs, organisation claim formatter, scope enforcement. |
| Admin APIs | `Identity.Base.Admin` | ✅ (for back-office) | Ships `/admin/users` & `/admin/roles` endpoints; organisation management is **not** implemented yet. |

> Skip any optional layer only if the corresponding capability is not needed. The organisation scenario assumes all four are present.

## Implementation Checklist

- [ ] **Wire Identity Base**
  - `services.AddIdentityBase(configuration, environment)` and `app.MapApiEndpoints()`.
  - Ensure OpenIddict clients/apps that will consume organisation context can refresh tokens (e.g., using refresh tokens or cookie re-issue).

- [ ] **Enable RBAC**
  - `var rolesBuilder = services.AddIdentityRoles(configuration);`
  - Choose storage (`rolesBuilder.AddDbContext<IdentityRolesDbContext>(...)`).
  - Seed definitions on startup via `await app.Services.SeedIdentityRolesAsync();`.
  - Define permissions covering organisation management (e.g. `organisations.manage`, `organisation.members.manage`, etc.).

- [ ] **Add Organisations Package**
  - `var orgBuilder = services.AddIdentityBaseOrganisations(options => options.UseNpgsql(...));`
  - Map endpoints with `app.MapIdentityBaseOrganisationEndpoints();`.
  - Configure metadata as needed (`ConfigureOrganisationModel`, `AfterOrganisationSeed`, custom scope resolver/claim formatter overrides).
  - Decide how the default org roles (`OrgOwner`, `OrgManager`, `OrgMember`) map to RBAC permissions.

- [ ] **Registration Flow Enhancements**
  - Extend the post-registration pipeline to call `IOrganisationService.CreateAsync(...)` and create a membership via `IOrganisationMembershipService.AddMemberAsync(...)`.
  - Assign the creator the `OrgOwner` (organisation admin) role. Optionally mark the membership as `IsPrimary` for default context.
  - Trigger token/cookie refresh so the new organisation context appears in claims.

- [ ] **Invitation & Membership Management**
  - Use organisation endpoints:
    - `POST /organisations/{orgId}/members` to invite/link users (requires permission + scope).
    - `PUT /organisations/{orgId}/members/{userId}` to update role assignments / primary flag.
    - `DELETE /organisations/{orgId}/members/{userId}` to remove members.
  - **Gap**: The package does not implement email/token invitation flows. Apps must build the invite UX (create pending user + send link, or provision user directly and email instructions).

- [ ] **Organisation Role Management**
  - Organisation admins call `GET/POST/DELETE /organisations/{orgId}/roles` for custom roles.
  - `GET /organisations/{orgId}/roles/{roleId}/permissions` returns inherited vs. explicit permission assignments; `PUT` replaces the explicit list stored in `Identity_OrganisationRolePermissions`.
  - Default permission bundles can be configured via `OrganisationRoleOptions.DefaultRoles`; per-organisation overrides flow through the same service layer and are surfaced in tokens/claims.

- [ ] **User Self-Service**
  - Existing Identity Base endpoints handle profile updates, password changes, MFA enable/disable. No extra work needed beyond exposing the routes in the client application.

- [ ] **Organisation Context Handling**
  - Call `GET /users/me/organisations` to show available orgs.
  - Call `POST /users/me/organisations/active` when the user switches orgs; refresh the sign-in so the new `org:*` claims propagate downstream.

- [ ] **Admin Application**
  - Use `Identity.Base.Admin` for user & role management (`services.AddIdentityAdmin(...)`, `app.MapIdentityAdminEndpoints()`).
  - **Gap**: There is currently no `/admin/organisations` surface. Global admins must rely on public organisation APIs or custom extensions for org CRUD/membership oversight.

## Configuration Notes

- **Authorization**: Organisations endpoints enforce permissions via `RequireOrganisationPermission("...")` and rely on `OrganisationScopeResolver` to ensure the acting user belongs to the target organisation. Override the resolver when building tenant-aware or super-admin flows.
- **Claims**: Tokens/cookies only include organisation info after the context accessor has a value. Ensure your client orchestrates a refresh post-switch.
- **Tenant Support**: `TenantId` columns are optional in OSS. If multi-tenant behavior is required, supply a custom scope resolver/claim formatter that includes tenant context and enforces tenant-level constraints.

## Known Gaps / Required Extensions

| Area | Gap | Suggested Action |
| --- | --- | --- |
| Invitations | No built-in invitation tokens/email workflows. | Build a custom endpoint/workflow that issues invite codes and creates users + memberships upon accept. |
| Org ↔ RBAC binding | Explicit permission overrides are persisted, but hosts must still decide which permissions correspond to new org roles. | Define default permission bundles in configuration/seed callbacks and expose admin UX (see sample) for fine-grained overrides. |
| Admin UI | No admin endpoints for listing/editing organisations. | Extend `Identity.Base.Admin` or create a companion package to expose `/admin/organisations` and related tooling. |
| Integration Tests | Minimal APIs currently validated by unit tests only. | Add WebApplicationFactory-based tests to cover authorization and org-switch flows end-to-end. |

Use this checklist as a blueprint when standing up a new multi-organisation application. Each unchecked gap represents work the host application (or a future OSS contribution) must address before the scenario is fully supported.
