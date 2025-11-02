
# Organisation Onboarding Flow (Registration → Invites → Member Management)

This guide walks through the end-to-end workflow for multi-organisation SaaS applications built on Identity Base and explains how to implement the flow in a new React app. You will install the required NuGet packages, wire the server, and integrate the React client for registration, invitations, and ongoing membership management.

## 1. Prerequisites

Install the following NuGet packages in your ASP.NET Core host:

| Package | Purpose |
| --- | --- |
| `Identity.Base` | Core identity + OpenIddict services, MFA, email confirmation/reset APIs. |
| `Identity.Base.Roles` | Role/permission catalog, seeding helpers, permission claim formatting. |
| `Identity.Base.Organisations` | Organisation domain model, membership + role APIs, organisation claim/scoping. |
| `Identity.Base.Admin` (optional) | Admin API for users/roles (global operations). |
| `Identity.Base.Email.MailJet` (optional) | Mailjet email delivery (registration invites, MFA emails). |

React client dependencies for a new app:
- `@identity-base/react-client` – core auth provider/hooks.
- `@identity-base/react-organisations` – organisation context, member lists, role helpers.

## 2. Server Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
identity.UseMailJetEmailSender(); // optional

var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var orgsBuilder = builder.Services.AddIdentityBaseOrganisations(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

var app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganisationEndpoints();
await app.RunAsync();
```

**Seeding**
- Call `await app.Services.SeedIdentityRolesAsync();` to apply permission/role defaults (e.g., `OrgOwner`, `OrgManager`, `OrgMember`, custom permissions like `organisation.members.manage`).
- Configure organisation hooks (`AfterOrganisationSeed`, `ConfigureOrganisationModel`) for custom metadata/roles if needed.
- Ensure OpenIddict clients support refresh tokens so new org claims reach tokens after registration.

## 3. Registration → Organisation Creation

**Server flow:**
1. Collect organisation metadata (e.g., name, slug) during registration in your API or SPA.
2. After `AddIdentityBase` creates the user, call `IOrganisationService.CreateAsync` to provision the organisation.
3. Add the registering user as a member via `IOrganisationMembershipService.AddMemberAsync`, assign `OrgOwner` (or equivalent), mark as primary.
4. Trigger `SignInManager.RefreshSignInAsync` so new org claims propagate to tokens/cookies.
5. Optional: create default org metadata via `AfterOrganisationSeed` callback, e.g., seeded profile fields.

Sample (simplified controller snippet):
```csharp
var org = await organisationService.CreateAsync(new OrganisationCreateRequest {
    Name = request.OrganisationName,
    Slug = request.OrganisationSlug
});

await organisationMembershipService.AddMemberAsync(org.Id, user.Id, new OrganisationMembershipRequest {
    RoleIds = { OrgRoles.Owner },
    IsPrimary = true
});

await signInManager.RefreshSignInAsync(user);
```

**React implementation:**
1. After successful registration (using `useRegister`), capture organisation metadata from the form.
2. Call a custom API route (e.g., `/api/organisations/create-from-registration`) that runs the logic above.
3. On success, redirect to dashboard; the identity provider will now include the new organisation in `useOrganisations()`.

## 4. Invitation Workflow

**Server flow:**
Identity Base exposes `POST /organisations/{orgId}/members` to add an existing user or provision a pending membership. Hosts typically:

1. Generate an invite token and store it (table keyed by user email + organisation).
2. Send email via Mailjet/SMTP containing `inviteId` + `token`.
3. When the recipient accepts, create the user if needed, then call the membership endpoint with the target user id.
4. After acceptance, refresh the user session to include the new organisation.

Example invite handler in ASP.NET Minimal API:
```csharp
app.MapPost("/organisations/{orgId:guid}/invites", async (
    Guid orgId,
    InviteRequest request,
    IOrganisationMembershipService membershipService,
    IInviteService inviteService) =>
{
    var invite = await inviteService.CreateInviteAsync(orgId, request.Email, request.RoleIds);
    await membershipService.AddPendingMemberAsync(invite);
    await inviteService.SendEmailAsync(invite);
    return Results.Accepted();
});
```

**React implementation:**
1. Build an Invite Members page using `useOrganisations().client.listMembers()` for context if needed.
2. Submit invites to your API. Upon acceptance (e.g., landing on `/invites/accept?inviteId=...`), the SPA:
   - Calls an endpoint to validate the invite and create a membership.
   - On success, logs in or refreshes tokens (`authManager.refreshTokens()`), and redirects to the organisation dashboard.
3. Provide UI for resend/cancel invitations (custom endpoints).

**Note:** The OSS package does not include invite token/email flow—implement pending invite storage, expiration, and email templates in your host.

## 5. Managing Members & Organisation Roles

Server endpoints and flows:
- `GET /organisations/{orgId}/members` – list memberships.
- `POST /organisations/{orgId}/members` – invite/add member.
- `PUT /organisations/{orgId}/members/{userId}` – update roles (`RoleIds`) and primary flag.
- `DELETE /organisations/{orgId}/members/{userId}` – remove membership.
- `GET/POST/DELETE /organisations/{orgId}/roles` – manage org-specific roles.
- `GET/PUT /organisations/{orgId}/roles/{roleId}/permissions` – inspect/update permission overrides (merges with global RBAC definitions).

Switching active organisation:
```http
POST /users/me/organisations/active
{ "organisationId": "..." }
```
If response indicates `requiresTokenRefresh`, invoke `IdentityAuthManager.refreshTokens()` client-side.

**React member management UI:**
- Use `useOrganisationMembers(orgId)` to render lists and perform updates:
```tsx
const { members, updateMember, removeMember, isLoading } = useOrganisationMembers(activeOrgId)

async function setRoles(userId: string, roles: string[]) {
  await updateMember(userId, { roleIds: roles })
}

async function remove(userId: string) {
  await removeMember(userId)
}
```
- Use `useOrganisations().client.getRolePermissions` and `.updateRolePermissions` to show role permissions editor.

## 6. React Client Setup

Initialize the app:
```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { OrganisationsProvider } from '@identity-base/react-organisations'

export function Root() {
  return (
    <IdentityProvider config={identityConfig}>
      <OrganisationsProvider apiBase={identityConfig.apiBase}>
        <App />
      </OrganisationsProvider>
    </IdentityProvider>
  )
}
```

Key hooks (from `@identity-base/react-organisations`):
- `useOrganisations()` – memberships, active org, `switchActiveOrganisation`, errors/loading.
- `useOrganisationSwitcher()` – convenience wrapper to call `POST /users/me/organisations/active` and refresh tokens.
- `useOrganisationMembers(orgId)` – paginated member list with `updateMember`, `removeMember` helpers.
- `useOrganisations().client` – typed client with `getRolePermissions`, `updateRolePermissions`, membership CRUD.

### Page-by-page guidance for a new React app

1. **Registration Form** – collect org name/slug in addition to user credentials. After calling `authManager.register`, call a custom endpoint to create the org and membership, then call `authManager.refreshTokens()`.
2. **Dashboard** – use `useOrganisations()` to show the active org, with a switcher (e.g., dropdown). When the user selects another org, call `switchActiveOrganisation` and await the refresh.
3. **Invite Members Page** – gather email + role list, POST to invite API. Display existing invites/members using `useOrganisationMembers`.
4. **Accept Invite Page** – unauthenticated route reading `inviteId` from query string. After verifying invite, either capture new user registration or sign in existing user, then redirect to dashboard with refreshed session.
5. **Member Management Page** – list members via `useOrganisationMembers`, provide UI to change roles/remove. Use `updateMember` and `removeMember` helpers.
6. **Role Permissions Page** – fetch roles via `useOrganisations().client.listRoles(orgId)` and use permission helpers to edit overrides, calling `updateRolePermissions`.

## 7. Sample Projects & References

- `apps/org-sample-api` – ASP.NET host wiring Identity Base packages, organisation seeding, invite APIs.
- `apps/org-sample-client` – React SPA demonstrating registration, invite acceptance, membership management using the React packages.
- Documentation:
  - [`docs/guides/organisation-admin-use-case.md`](organisation-admin-use-case.md)
  - [`docs/reference/React_Organisations_AddOn.md`](../reference/React_Organisations_AddOn.md)
  - [`docs/guides/full-stack-integration-guide.md`](full-stack-integration-guide.md)

## 8. Next Steps & Gaps

| Area | Considerations |
| --- | --- |
| Admin UI | OSS admin package covers users/roles only. Build `/admin/organisations` if global admins need org oversight. |
| Invitations | Implement pending invite storage, expiration, and email templates (Mailjet/other provider). |
| Auditing | Hook into `IAuditLogger` to capture membership changes per organisation. |
| Tenant Context | Override `OrganisationScopeResolver` for multi-tenant gating if required. |

With these pieces in place, registration flows automatically create an organisation, organisation admins can invite and manage users, and React clients can display the correct organisation context across the app.
