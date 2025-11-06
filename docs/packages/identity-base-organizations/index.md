# Identity.Base.Organizations

## Overview
`Identity.Base.Organizations` adds multi-organization support on top of the Identity Base + RBAC stack. It ships EF Core aggregates for organizations, memberships, and organization-specific roles; hosted migration/seed services; Minimal APIs for CRUD operations; and middleware/helpers for flowing the active organization through requests and tokens. Invitations, membership management, and organization-level permission overrides are all handled within this package.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Organizations
```

Register services after the core and roles packages:

```csharp
using Identity.Base.Extensions;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Organizations.Data;
using Microsoft.EntityFrameworkCore;

builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var orgsBuilder = builder.Services.AddIdentityBaseOrganizations(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.UseOrganizationContextFromHeader();                // reads X-Organization-Id
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
await app.RunAsync();
```

`OrganizationMigrationHostedService` applies any pending migrations at startup, and `OrganizationSeedHostedService` provisions default roles (`OrgOwner`, `OrgManager`, `OrgMember`). These roles receive the user-scoped permissions (`user.organizations.*`) only—hosts remain in control of any platform-wide `admin.organizations.*` roles.

## Configuration

- `OrganizationOptions` – slug/display name length limits, metadata size caps.
- `OrganizationRoleOptions` – default role definitions, system role names, description lengths.
- Use `orgsBuilder.ConfigureOrganizationModel(...)` to apply additional EF Core configuration (indexes, value converters).
- Seed hooks: `orgsBuilder.AfterOrganizationSeed(...)` for post-seeding provisioning (e.g., billing setup, tenant metadata).

Connection strings can be supplied via the `IdentityOrganizations` named connection or explicitly through the options callback shown above.

## Public Surface

### Minimal APIs

| Route | Description | Permission |
| --- | --- | --- |
| `GET /admin/organizations` | Paged list of organizations; supports `tenantId`, `status`, `page`, `pageSize`, `search`, `sort`. Returns `PagedResult<OrganizationDto>`. | `admin.organizations.read` |
| `POST /admin/organizations` | Create a new organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{id}` | Retrieve organization details. | `admin.organizations.read` |
| `PATCH /admin/organizations/{id}` | Update display name, metadata, status. | `admin.organizations.manage` |
| `DELETE /admin/organizations/{id}` | Archive an organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{id}/members` | Paged member list with role ids and metadata (`page`, `pageSize`, `search`, `roleId`, `isPrimary`, `sort`). Returns `PagedResult<OrganizationMembershipDto>`. | `admin.organizations.members.read` |
| `POST /admin/organizations/{id}/members` | Add an existing user directly (no invite). | `admin.organizations.members.manage` |
| `PUT /admin/organizations/{id}/members/{userId}` | Update member roles / primary flag. | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{id}/members/{userId}` | Remove a member. | `admin.organizations.members.manage` |
| `GET /admin/organizations/{id}/roles` | Paged list of system + org-specific roles (`page`, `pageSize`, `search`, `sort`). Returns `PagedResult<OrganizationRoleDto>`. | `admin.organizations.roles.read` |
| `POST /admin/organizations/{id}/roles` | Create an organization-specific role. | `admin.organizations.roles.manage` |
| `DELETE /admin/organizations/{id}/roles/{roleId}` | Delete an organization role. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{id}/roles/{roleId}/permissions` | Retrieve effective vs. explicit permissions. | `admin.organizations.roles.read` |
| `PUT /admin/organizations/{id}/roles/{roleId}/permissions` | Replace explicit permission overrides. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{id}/invitations` | Paged list of active invitations (`page`, `pageSize`, `search`, `sort`). Returns `PagedResult<OrganizationInvitationDto>`. | `admin.organizations.members.manage` |
| `POST /admin/organizations/{id}/invitations` | Create an invitation (stores token + metadata; host is responsible for emailing the code). | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{id}/invitations/{code}` | Revoke an invitation. | `admin.organizations.members.manage` |
| `GET /invitations/{code}` | Public endpoint to validate invite metadata. | Anonymous |
| `POST /invitations/claim` | Accept an invite (authenticated) and add membership. | Authenticated user matching invite email |
| `GET /users/me/organizations` | Paged list of the caller's memberships (`page`, `pageSize`, `search`, `sort`, `includeArchived`). Returns `PagedResult<UserOrganizationMembershipDto>`. | Authenticated |

All paged endpoints honor the shared query parameters: `page` (default 1), `pageSize` (default 25, max 200), `search` (full-text match on supported fields), and `sort` (comma-delimited `field[:asc|:desc]`). The Minimal APIs normalize these values through `PageRequest` and always return a `PagedResult<T>` payload (`page`, `pageSize`, `totalCount`, `items`). Admin routes also surface endpoint-specific filters such as `tenantId`, `status`, `roleId`, or `includeArchived`.

### Services & Helpers
- `IOrganizationService` – organization CRUD operations.
- `IOrganizationMembershipService` – membership queries, add/update/remove members.
- `IOrganizationRoleService` – organization role management.
- `OrganizationInvitationService` – invitation lifecycle (create/list/revoke/accept).
- `IOrganizationContextAccessor` / `OrganizationContextFromHeaderMiddleware` – current organization context.
- Active organization selection is client-driven; there is no `POST /users/me/organizations/active` endpoint. Send the header to scope non-admin routes.
- Claims augmentors and additional permission sources that enrich identity tokens with org membership data.
- To activate an organization for subsequent requests, send the `X-Organization-Id` header with the desired organization id (the admin endpoints ignore this header by design).

#### Invitation acceptance flow

```bash
# 1. SPA fetches invitation metadata
curl https://identity.example.com/invitations/8ed0e3c8-41c5-4e1c-b3d6-6ec9c7deef6d

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
