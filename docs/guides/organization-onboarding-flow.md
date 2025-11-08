
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

await organizationMembershipService.AddMemberAsync(new OrganizationMembershipRequest {
    OrganizationId = org.Id,
    UserId = user.Id,
    RoleIds = new[] { OrgRoles.Owner }
});

await signInManager.RefreshSignInAsync(user);
```

**React implementation:**
1. After successful registration (using `useRegister`), capture organization metadata from the form.
2. Call a custom API route (e.g., `/api/organizations/create-from-registration`) that runs the logic above.
3. On success, redirect to dashboard; the identity provider will now include the new organization in `useOrganizations()`.

## 4. Invitation Workflow

**Server flow:**
Identity Base includes invitation storage, token issuance, expiration, and acceptance APIs. A typical flow looks like:

1. Call `POST /admin/organizations/{orgId}/invitations` (or invoke `OrganizationInvitationService.CreateAsync`) to create the invite. The service enforces uniqueness, persists the record, and returns the invite `code`, expiry, and organization metadata.
2. Use the response to send an email (MailJet/SMTP/etc.) that links the recipient to your SPA (e.g., `/invitations/accept?code=...`).
3. When the recipient lands on the SPA, fetch `GET /invitations/{code}` to show organization details. After the user signs in, call `POST /invitations/claim` with the code – this automatically adds the membership, merges roles, deletes the invite, and returns `RequiresTokenRefresh = true`.
4. Refresh the caller’s tokens/cookies so the new organization appears in claims and the React providers.

If you already know the target user identifier (no invite needed), you can still call `POST /admin/organizations/{orgId}/members` directly to add them immediately.

Example invite handler in ASP.NET Minimal API:
```csharp
app.MapPost("/admin/organizations/{orgId:guid}/invitations", async (
    Guid orgId,
    CreateOrganizationInvitationRequest request,
    ClaimsPrincipal principal,
    OrganizationInvitationService invitations,
    IEmailSender emailSender,
    CancellationToken cancellationToken) =>
{
    var actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var invite = await invitations.CreateAsync(
        orgId,
        request.Email,
        request.RoleIds ?? Array.Empty<Guid>(),
        actorId,
        request.ExpiresInHours,
        cancellationToken);

    await emailSender.SendInviteAsync(invite); // host-defined email delivery

    return Results.Created($"/admin/organizations/{orgId}/invitations/{invite.Code}", OrganizationApiMapper.ToInvitationDto(invite));
});
```

**React implementation:**
1. Build an Invite Members page that calls `/admin/organizations/{orgId}/invitations` and lists pending invites via `/admin/organizations/{orgId}/invitations` GET (paged result with `items`; accepts `page`, `pageSize`, `search`, `sort`).
2. The acceptance route (`/invitations/accept?code=...`) should:
   - Call `GET /invitations/{code}` to validate and display org details.
   - Ensure the user is authenticated (register or log in if required).
   - Call `POST /invitations/claim` and, on success, invoke `authManager.refreshTokens()` before redirecting to the dashboard.
3. Provide actions to resend or revoke invitations via the same invitation endpoints.

**Note:** Identity Base already ships invite storage, token generation, expiration handling, and acceptance endpoints (`OrganizationInvitationService`, `/admin/organizations/{id}/invitations`, `/invitations/claim`). You still own the outer workflow: trigger the service, send the email (MailJet/other provider), and surface an invite acceptance UI in your app.

## 5. Managing Members & Organization Roles

Server endpoints and flows:
- `GET /admin/organizations/{orgId}/members` – list memberships (paged result, supports `page`, `pageSize`, `search`, `roleId`, `sort`).
- `POST /admin/organizations/{orgId}/members` – add an existing user immediately (no invite flow).
- `PUT /admin/organizations/{orgId}/members/{userId}` – update roles (`RoleIds`).
- `DELETE /admin/organizations/{orgId}/members/{userId}` – remove membership.
- `GET/POST/DELETE /admin/organizations/{orgId}/roles` – manage org-specific roles (the list route returns `PagedResult<OrganizationRoleDto>` and honors `page`, `pageSize`, `search`, `sort`).
- `GET/PUT /admin/organizations/{orgId}/roles/{roleId}/permissions` – inspect/update permission overrides (merges with global RBAC definitions).

Switching active organization: send the `X-Organization-Id` header on subsequent API requests (there is no dedicated endpoint to change the active org). If membership changes alter the user's permission set, call `IdentityAuthManager.refreshTokens()` to refresh the identity token.

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
- `useOrganizationSwitcher()` – convenience wrapper that updates the active organization id (persisted locally) and refreshes memberships/tokens when needed.
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
  - [`docs/packages/identity-base-react-organizations/index.md`](../packages/identity-base-react-organizations/index.md)
  - [`docs/guides/full-stack-integration-guide.md`](full-stack-integration-guide.md)

## 8. Next Steps & Gaps

| Area | Considerations |
| --- | --- |
| Admin UI | Ensure admin surfaces call `/admin/organizations/...` endpoints and grant the `admin.organizations.*` permissions to operators. |
| Invitations | Implement pending invite storage, expiration, and email templates (Mailjet/other provider). |
| Auditing | Hook into `IAuditLogger` to capture membership changes per organization. |
| Tenant Context | Override `OrganizationScopeResolver` for multi-tenant gating if required. |

With these pieces in place, registration flows automatically create an organization, organization admins can invite and manage users, and React clients can display the correct organization context across the app.
