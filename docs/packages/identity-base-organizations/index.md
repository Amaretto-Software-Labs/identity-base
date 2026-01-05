# Identity.Base.Organizations

## Overview
`Identity.Base.Organizations` adds multi-organization support on top of the Identity Base + RBAC stack. It ships EF Core aggregates for organizations, memberships, and organization-specific roles; hosted seed services; Minimal APIs for CRUD operations; and middleware/helpers for flowing the active organization through requests and tokens. Invitations, membership management, and organization-level permission overrides are all handled within this package.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Organizations
```

Register services after the core and roles packages:

```csharp
using Identity.Base.Extensions;
using Identity.Base.Organizations.Extensions;
using Microsoft.EntityFrameworkCore;

Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext = (sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Primary")
        ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

    options.UseNpgsql(connectionString); // or UseSqlServer(connectionString)
};

builder.Services.AddIdentityBase(builder.Configuration, builder.Environment, configureDbContext: configureDbContext);
builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext);
builder.Services.AddIdentityBaseOrganizations(configureDbContext)
    .UseTablePrefix("Contoso"); // optional: use the same prefix as the core/RBAC tables

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.UseOrganizationContextFromHeader();                // reads X-Organization-Id
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
await app.RunAsync();
```

`OrganizationSeedHostedService` provisions default roles (`OrgOwner`, `OrgManager`, `OrgMember`) after your host applies migrations. These roles receive the user-scoped permissions (`user.organizations.*`) only—hosts remain in control of any platform-wide `admin.organizations.*` roles.

## Configuration

- `OrganizationOptions` – slug/display name length limits, metadata size caps.
- `OrganizationRoleOptions` – default role definitions, system role names, description lengths.
- Use `orgsBuilder.ConfigureOrganizationModel(...)` to apply additional EF Core configuration (indexes, value converters).
- Seed hooks: `orgsBuilder.AfterOrganizationSeed(...)` for post-seeding provisioning (e.g., billing setup, tenant metadata).

Connection strings must be supplied via your own DbContext registrations or the delegate shown above; the package no longer infers `IdentityOrganizations` automatically.

### Migrations
This package no longer ships EF Core migrations. Generate them from your host project so they target your selected provider (PostgreSQL, SQL Server, etc.):

```bash
dotnet ef migrations add InitialOrganizations --context OrganizationDbContext
dotnet ef database update --context OrganizationDbContext
```

Apply these migrations (for example during deploy or CI) before the hosted seed service runs.

## Public Surface

### Minimal APIs

| Route | Description | Permission |
| --- | --- | --- |
| `GET /admin/organizations` | Paged list of organizations; supports `tenantId`, `status`, `page`, `pageSize`, `search`, `sort`. Returns `PagedResult<OrganizationDto>`. | `admin.organizations.read` |
| `POST /admin/organizations` | Create a new organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{id}` | Retrieve organization details. | `admin.organizations.read` |
| `PATCH /admin/organizations/{id}` | Update display name, metadata, status. | `admin.organizations.manage` |
| `DELETE /admin/organizations/{id}` | Archive an organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{id}/members` | Paged member list with role ids and metadata (`page`, `pageSize`, `search`, `roleId`, `sort`). Returns `PagedResult<OrganizationMembershipDto>`. | `admin.organizations.members.read` |
| `POST /admin/organizations/{id}/members` | Add an existing user directly (no invite). | `admin.organizations.members.manage` |
| `PUT /admin/organizations/{id}/members/{userId}` | Update member roles. | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{id}/members/{userId}` | Remove a member. | `admin.organizations.members.manage` |
| `GET /admin/organizations/{id}/roles` | Paged list of system + org-specific roles (`page`, `pageSize`, `search`, `sort`). Returns `PagedResult<OrganizationRoleDto>`. | `admin.organizations.roles.read` |
| `POST /admin/organizations/{id}/roles` | Create an organization-specific role. | `admin.organizations.roles.manage` |
| `DELETE /admin/organizations/{id}/roles/{roleId}` | Delete an organization role. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{id}/roles/{roleId}/permissions` | Retrieve effective vs. explicit permissions. | `admin.organizations.roles.read` |
| `PUT /admin/organizations/{id}/roles/{roleId}/permissions` | Replace explicit permission overrides. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{id}/invitations` | Paged list of active invitations (`page`, `pageSize`, `search`, `sort`). Returns `PagedResult<OrganizationInvitationDto>`. | `admin.organizations.members.manage` |
| `POST /admin/organizations/{id}/invitations` | Create an invitation (stores token + metadata; host is responsible for emailing the code). | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{id}/invitations/{code}` | Revoke an invitation. | `admin.organizations.members.manage` |
| `GET /invitations/{code}` | Public endpoint to validate an invite code and return organization preview metadata (no email/role ids). | Anonymous |
| `POST /invitations/claim` | Accept an invite (authenticated) and add membership. | Authenticated user matching invite email |
| `POST /users/me/organizations` | Create a new organization owned by the caller; seeds the default owner role membership. | Authenticated |
| `GET /users/me/organizations` | Paged list of the caller's memberships (`page`, `pageSize`, `search`, `sort`, `includeArchived`). Returns `PagedResult<UserOrganizationMembershipDto>`. | Authenticated |
| `GET /users/me/organizations/{id}` | Retrieve organization details scoped to the caller’s membership. | `user.organizations.read` |
| `PATCH /users/me/organizations/{id}` | Update display name/metadata for the caller’s organization. | `user.organizations.manage` |
| `GET/POST/PUT/DELETE /users/me/organizations/{id}/members` | Member management within the caller’s organization (paged list + CRUD). | `user.organizations.members.*` |
| `GET/POST/DELETE /users/me/organizations/{id}/roles` | Manage organization-specific roles plus their permissions (`GET/PUT /roles/{roleId}/permissions`). | `user.organizations.roles.*` |
| `GET/POST/DELETE /users/me/organizations/{id}/invitations` | Issue and manage invitations scoped to the caller’s organization. | `user.organizations.members.manage` |

All paged endpoints honor the shared query parameters: `page` (default 1), `pageSize` (default 25, max 200), `search` (full-text match on supported fields), and `sort` (comma-delimited `field[:asc|:desc]`). The Minimal APIs normalize these values through `PageRequest` and always return a `PagedResult<T>` payload (`page`, `pageSize`, `totalCount`, `items`). Admin routes also surface endpoint-specific filters such as `tenantId`, `status`, `roleId`, or `includeArchived`. User-facing endpoints reuse the exact same query contract, so front ends can apply identical pagination helpers regardless of which surface they call.

### Services & Helpers
- `IOrganizationService` – organization CRUD operations.
- `IOrganizationMembershipService` – membership queries, add/update/remove members.
- `IOrganizationRoleService` – organization role management.
- `OrganizationInvitationService` – invitation lifecycle (create/list/revoke/accept).
- `OrganizationClaimsPrincipalExtensions` – helpers for reading org claim context and membership (`org:id`, `org:memberships`).
- `IOrganizationContextAccessor` / `OrganizationContextFromHeaderMiddleware` – current organization context.
- Active organization selection is client-driven; there is no `POST /users/me/organizations/active` endpoint. Send the header to scope non-admin routes.
- Claims augmentors and additional permission sources that enrich identity tokens with org membership data.
- To activate an organization for subsequent requests, send the `X-Organization-Id` header with the desired organization id (the admin endpoints ignore this header by design).

#### Invitation acceptance flow

```bash
# 1. SPA fetches invitation metadata
curl https://identity.example.com/invitations/8ed0e3c8-41c5-4e1c-b3d6-6ec9c7deef6d

# Response (preview)
{
  "code": "...",
  "organizationSlug": "acme",
  "organizationName": "Acme Corp",
  "expiresAtUtc": "..."
}

# 2. Authenticated user claims the invitation
curl -X POST https://identity.example.com/invitations/claim \
     -H "Authorization: Bearer $ACCESS_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{ "code": "8ed0e3c8-41c5-4e1c-b3d6-6ec9c7deef6d" }'

# Response
{
  "organizationId": "...",
  "organizationSlug": "acme",
  "organizationName": "Acme Corp",
  "roleIds": ["..."],
  "wasExistingMember": false,
  "wasExistingUser": true,
  "requiresTokenRefresh": true
}
```

## Extension Points

- `orgsBuilder.AddOrganizationScopeResolver<TResolver>()` – override the membership scope checks (e.g., allow tenant-wide admins).
- `orgsBuilder.AddOrganizationClaimFormatter<TFormatter>()` – change how org metadata is serialized into claims.
- `orgsBuilder.ConfigureOrganizationModel(...)` – apply EF Core customizations.
- `orgsBuilder.AfterOrganizationSeed(...)` – run additional provisioning after default roles are created.
- `IdentityBaseOrganizationsBuilder.AddOrganizationLifecycleListener<TListener>()` – implement `IOrganizationLifecycleListener` once to observe/veto organization lifecycle events (create/update/archive/restore, invitation created/revoked/accepted, membership add/update/remove). Legacy listener registrations (`AddOrganizationCreationListener`, etc.) still work via shims.
- `orgsBuilder.AddOrganizationScopeResolver<TResolver>()` – override the membership scope checks (e.g., allow tenant-wide admins).
- Implement custom invitation stores by replacing `IOrganizationInvitationStore`.

## Dependencies & Compatibility

- Depends on `Identity.Base` (core identity) and `Identity.Base.Roles` (permission catalog).
- React integrations use `@identity-base/react-organizations` to interact with the endpoints exposed here.
- Invitation acceptance relies on authenticated users; pair with Identity Base login/registration flows.

## Troubleshooting & Tips
- **Missing organization context** – ensure `app.UseOrganizationContextFromHeader()` is registered *before* `MapIdentityBaseOrganizationEndpoints()` and that the SPA sends the `X-Organization-Id` header for non-admin routes.
- **Header set for admin routes** – admin endpoints intentionally ignore the header and operate on all organizations; do not expect the header to scope `/admin/organizations`.
- **Invitation emails** – the package persists invitations but does not send email. Call `OrganizationInvitationService.CreateAsync` then hand the returned record to your email infrastructure (e.g., Mailjet sender) using the `Code` property.
- **RequiresTokenRefresh` = true** – when the claim endpoint responds with `RequiresTokenRefresh`, instruct the SPA to call `IdentityAuthManager.refreshTokens()` so the new organization membership is reflected in tokens.

## Examples & Guides

- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Sample API: `apps/org-sample-api`
- Sample SPA: `apps/org-sample-client`
- Playbook: ../../playbooks/seed-roles-and-default-organization.md
- Playbook: ../../playbooks/organization-invitation-flow.md

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Organizations` entries)
