
# Organization Onboarding Flow (Registration → Invites → Member Management)

This guide walks through the end-to-end workflow for multi-organization SaaS applications built on Identity Base and explains how to implement the flow in a new React app. You will install the required NuGet packages, wire the server, and integrate the React client for registration, invitations, and ongoing membership management.

## 1. Prerequisites

Install the following NuGet packages in your ASP.NET Core host:

| Package | Purpose |
| --- | --- |
| `Identity.Base` | Core identity + OpenIddict services, MFA, email confirmation/reset APIs. |
| `Identity.Base.Roles` | Role/permission catalog, seeding helpers, permission claim formatting. |
| `Identity.Base.Organizations` | Organization domain model, membership + role APIs, organization claim/scoping. |
| `Identity.Base.Admin` (optional) | Admin API for users/roles (global operations). |
| `Identity.Base.Email.MailJet` (optional) | Mailjet email delivery (registration invites, MFA emails). |

React client dependencies for a new app:
- `@identity-base/react-client` – core auth provider/hooks.
- `@identity-base/react-organizations` – organization context, member lists, role helpers.

## 2. Server Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
identity.UseMailJetEmailSender(); // optional

var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var orgsBuilder = builder.Services.AddIdentityBaseOrganizations(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
await app.RunAsync();
```

**Seeding**
- Call `await app.Services.SeedIdentityRolesAsync();` to apply permission/role defaults (e.g., `OrgOwner`, `OrgManager`, `OrgMember`). The built-in roles now carry user-scoped permissions (`user.organizations.*`). Grant `admin.organizations.*` to a separate role if you need platform-wide actions.
- Add `app.UseOrganizationContextFromHeader();` to the host and send the `X-Organization-Id` header from the SPA so each request selects the active organization without refreshing tokens on every switch. Refresh tokens only when membership changes (e.g., an owner is added or removed).
- Configure organization hooks (`AfterOrganizationSeed`, `ConfigureOrganizationModel`) for custom metadata/roles if needed.
- Ensure OpenIddict clients support refresh tokens so new org claims reach tokens after registration.

## 3. Registration → Organization Creation

**Server flow:**
1. Collect organization metadata (e.g., name, slug) during registration in your API or SPA.
2. After `AddIdentityBase` creates the user, call `IOrganizationService.CreateAsync` to provision the organization.
3. Add the registering user as a member via `IOrganizationMembershipService.AddMemberAsync`, assign `OrgOwner` (or equivalent), mark as primary.
4. Trigger `SignInManager.RefreshSignInAsync` so new org claims propagate to tokens/cookies.
5. Optional: create default org metadata via `AfterOrganizationSeed` callback, e.g., seeded profile fields.

Sample (simplified controller snippet):
```csharp
var org = await organizationService.CreateAsync(new OrganizationCreateRequest {
    Name = request.OrganizationName,
    Slug = request.OrganizationSlug
});

await organizationMembershipService.AddMemberAsync(org.Id, user.Id, new OrganizationMembershipRequest {
    RoleIds = { OrgRoles.Owner },
    IsPrimary = true
});

await signInManager.RefreshSignInAsync(user);
```

**React implementation:**
1. After successful registration (using `useRegister`), capture organization metadata from the form.
2. Call a custom API route (e.g., `/api/organizations/create-from-registration`) that runs the logic above.
3. On success, redirect to dashboard; the identity provider will now include the new organization in `useOrganizations()`.

## 4. Invitation Workflow

**Server flow:**
Identity Base exposes `POST /organizations/{orgId}/members` to add an existing user or provision a pending membership. Hosts typically:

1. Generate an invite token and store it (table keyed by user email + organization).
2. Send email via Mailjet/SMTP containing `inviteId` + `token`.
3. When the recipient accepts, create the user if needed, then call the membership endpoint with the target user id.
4. After acceptance, refresh the user session to include the new organization.

Example invite handler in ASP.NET Minimal API:
```csharp
app.MapPost("/organizations/{orgId:guid}/invites", async (
    Guid orgId,
    InviteRequest request,
    IOrganizationMembershipService membershipService,
    IInviteService inviteService) =>
{
    var invite = await inviteService.CreateInviteAsync(orgId, request.Email, request.RoleIds);
    await membershipService.AddPendingMemberAsync(invite);
    await inviteService.SendEmailAsync(invite);
    return Results.Accepted();
});
```

**React implementation:**
1. Build an Invite Members page using `useOrganizations().client.listMembers()` for context if needed.
2. Submit invites to your API. Upon acceptance (e.g., landing on `/invites/accept?inviteId=...`), the SPA:
   - Calls an endpoint to validate the invite and create a membership.
   - On success, logs in or refreshes tokens (`authManager.refreshTokens()`), and redirects to the organization dashboard.
3. Provide UI for resend/cancel invitations (custom endpoints).

**Note:** The OSS package does not include invite token/email flow—implement pending invite storage, expiration, and email templates in your host.

## 5. Managing Members & Organization Roles

Server endpoints and flows:
- `GET /organizations/{orgId}/members` – list memberships.
- `POST /organizations/{orgId}/members` – invite/add member.
- `PUT /organizations/{orgId}/members/{userId}` – update roles (`RoleIds`) and primary flag.
- `DELETE /organizations/{orgId}/members/{userId}` – remove membership.
- `GET/POST/DELETE /organizations/{orgId}/roles` – manage org-specific roles.
- `GET/PUT /organizations/{orgId}/roles/{roleId}/permissions` – inspect/update permission overrides (merges with global RBAC definitions).

Switching active organization:
```http
POST /users/me/organizations/active
{ "organizationId": "..." }
```
If response indicates `requiresTokenRefresh`, invoke `IdentityAuthManager.refreshTokens()` client-side.

**React member management UI:**
- Use `useOrganizationMembers(orgId)` to render lists and perform updates:
```tsx
const { members, updateMember, removeMember, isLoading } = useOrganizationMembers(activeOrgId)

async function setRoles(userId: string, roles: string[]) {
  await updateMember(userId, { roleIds: roles })
}

async function remove(userId: string) {
  await removeMember(userId)
}
```
- Use `useOrganizations().client.getRolePermissions` and `.updateRolePermissions` to show role permissions editor.

## 6. React Client Setup

Initialize the app:
```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { OrganizationsProvider } from '@identity-base/react-organizations'

export function Root() {
  return (
    <IdentityProvider config={identityConfig}>
      <OrganizationsProvider apiBase={identityConfig.apiBase}>
        <App />
      </OrganizationsProvider>
    </IdentityProvider>
  )
}
```

Key hooks (from `@identity-base/react-organizations`):
- `useOrganizations()` – memberships, active org, `switchActiveOrganization`, errors/loading.
- `useOrganizationSwitcher()` – convenience wrapper to call `POST /users/me/organizations/active` and refresh tokens.
- `useOrganizationMembers(orgId)` – paginated member list with `updateMember`, `removeMember` helpers.
- `useOrganizations().client` – typed client with `getRolePermissions`, `updateRolePermissions`, membership CRUD.

### Page-by-page guidance for a new React app

1. **Registration Form** – collect org name/slug in addition to user credentials. After calling `authManager.register`, call a custom endpoint to create the org and membership, then call `authManager.refreshTokens()`.
2. **Dashboard** – use `useOrganizations()` to show the active org, with a switcher (e.g., dropdown). When the user selects another org, call `switchActiveOrganization` and await the refresh.
3. **Invite Members Page** – gather email + role list, POST to invite API. Display existing invites/members using `useOrganizationMembers`.
4. **Accept Invite Page** – unauthenticated route reading `inviteId` from query string. After verifying invite, either capture new user registration or sign in existing user, then redirect to dashboard with refreshed session.
5. **Member Management Page** – list members via `useOrganizationMembers`, provide UI to change roles/remove. Use `updateMember` and `removeMember` helpers.
6. **Role Permissions Page** – fetch roles via `useOrganizations().client.listRoles(orgId)` and use permission helpers to edit overrides, calling `updateRolePermissions`.

## 7. Sample Projects & References

- `apps/org-sample-api` – ASP.NET host wiring Identity Base packages, organization seeding, invite APIs.
- `apps/org-sample-client` – React SPA demonstrating registration, invite acceptance, membership management using the React packages.
- Documentation:
  - [`docs/guides/organization-admin-use-case.md`](organization-admin-use-case.md)
  - [`docs/reference/React_Organizations_AddOn.md`](../reference/React_Organizations_AddOn.md)
  - [`docs/guides/full-stack-integration-guide.md`](full-stack-integration-guide.md)

## 8. Next Steps & Gaps

| Area | Considerations |
| --- | --- |
| Admin UI | OSS admin package covers users/roles only. Build `/admin/organizations` if global admins need org oversight. |
| Invitations | Implement pending invite storage, expiration, and email templates (Mailjet/other provider). |
| Auditing | Hook into `IAuditLogger` to capture membership changes per organization. |
| Tenant Context | Override `OrganizationScopeResolver` for multi-tenant gating if required. |

With these pieces in place, registration flows automatically create an organization, organization admins can invite and manage users, and React clients can display the correct organization context across the app.
