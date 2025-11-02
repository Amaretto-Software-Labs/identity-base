# Organisation Sample API

This sample host demonstrates how the Identity Base packages compose to deliver a multi-organisation SaaS back end. It pulls together the core authentication service, RBAC, organisation APIs, and the lightweight admin surface, and adds a small amount of sample-only orchestration logic (bootstrap + invitations) so you can walk the end-to-end scenario described in `docs/guides/organisation-admin-use-case.md`.

## Features
- Identity Base registration/login, MFA, profile, and OpenIddict endpoints (`/auth/*`, `/connect/*`, etc.).
- Role catalogue + admin endpoints from `Identity.Base.Roles` and `Identity.Base.Admin`.
- Full organisation CRUD, membership, role management, and user context switching via `Identity.Base.Organisations`.
- Registration bootstrap: new users can provide `organisationSlug`/`organisationName` metadata and automatically become the owner of their organisation.
- In-memory invitation workflow (`/sample/organisations/{id}/invitations` + `/sample/invitations/claim`) to illustrate how organisation admins can invite additional members and assign org roles.

## Quick Start
```bash
dotnet restore
dotnet run --project apps/org-sample-api/OrgSampleApi.csproj
```

The sample expects PostgreSQL. Every subsystem (core identity, roles, organisations, invitations) shares the `ConnectionStrings:Primary` database. On startup the app calls `EnsureCreated` for the invitation schema; run the Identity Base migrations separately if the target database is fresh.

### Registration Payload
Use the existing Identity Base endpoint:

```
POST /auth/register
Content-Type: application/json

{
  "email": "owner@example.com",
  "password": "Passw0rd!Passw0rd!",
  "metadata": {
    "displayName": "Ada Lovelace",
    "organisationSlug": "lovelace-lab",
    "organisationName": "Lovelace Lab",
    "organisation.metadata.industry": "Research"
  }
}
```

The sample listener provisions:
- Organisation `lovelace-lab`
- Membership for the new user as primary member
- Org owner role assignment (uses the default `OrgOwner` role from the organisations package)

### Invitations
1. Authenticate as an organisation owner/manager.
2. Create an invite:
   ```
   POST /sample/organisations/{organisationId}/invitations
   {
     "email": "teammate@example.com",
     "roleIds": ["{orgRoleId}"],
     "expiresInHours": 48
   }
   ```
3. Share the returned `code` with the invitee.
4. Once they register and sign in, they can claim the code:
   ```
   POST /sample/invitations/claim
   {
     "code": "{invitationCode}"
   }
   ```
   The API attaches the user to the organisation (creating or updating membership) and returns metadata indicating a token refresh is required.

You can list or revoke invites with:
- `GET /sample/organisations/{organisationId}/invitations`
- `DELETE /sample/organisations/{organisationId}/invitations/{code}`

### Helpful Sample Endpoints
- `GET /sample/status` – health probe for the sample surface.
- `GET /sample/defaults` – shows the default organisation seeding metadata derived from configuration.
- `GET /sample/registration/profile-fields` – exposes the registration profile schema so a client can build the registration form dynamically.

## Notes
- Invitation persistence now comes from the shared `Identity.Base.Organisations` package. The hosted migration service applies the invitation schema alongside the other organisation tables on startup.
- No automated tests are provided for this sample host by design. Use it as a reference or starting point for your own integration tests.
- All admin and organisation endpoints require an authenticated user with the corresponding RBAC permissions; use the seeded admin user defined under `IdentitySeed` or create new roles as needed.
