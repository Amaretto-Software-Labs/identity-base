---
id: playbooks/seed-roles-and-default-organization
title: Seed Roles and Default Organization
version: 0.1.0
last_reviewed: 2025-11-05
tags: [roles, organizations, seeding]
required_roles: [Developer]
prerequisites:
  dotnet: "9.x"
  database: "PostgreSQL 16"
  repo_root: "cloned"
  env_files: [".env"]
required_secrets:
  - CONNECTIONSTRINGS__PRIMARY
---

# Goal
Enable RBAC and Organizations in the host, seed an admin user, define admin permissions, create a default organization, and assign the admin as a member. Verify via authenticated API calls.

# Preconditions
- Working directory at repo root.
- `.env` contains `CONNECTIONSTRINGS__PRIMARY` for local Postgres.
- Identity Host is used: `Identity.Base.Host` project.

# Resources
- Roles package: docs/packages/identity-base-roles/index.md
- Organizations package: docs/packages/identity-base-organizations/index.md
- Getting Started: docs/guides/getting-started.md

# Command Steps
Command: dotnet restore Identity.sln
```bash
dotnet restore Identity.sln
```

Command: dotnet build -c Debug Identity.sln
```bash
dotnet build -c Debug Identity.sln
```

Optional Step 3: Start Postgres and Mailhog
Command: docker compose -f docker-compose.local.yml up -d postgres mailhog
```bash
docker compose -f docker-compose.local.yml up -d postgres mailhog
```

# File Edits
- path: Identity.Base.Host/Program.cs
  - Insert after `identityBuilder.UseMailJetEmailSender();`:
    ```csharp
    using Identity.Base.Organizations.Extensions; // at top of file
    using Identity.Base.Organizations.Data;       // at top of file
    using Microsoft.EntityFrameworkCore;          // at top of file

    var organizationsBuilder = builder.Services.AddIdentityBaseOrganizations(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Primary")));

    identityBuilder.AfterIdentitySeed(async (sp, ct) =>
    {
        // Create default organization and add the seed admin as a member
        var orgService = sp.GetRequiredService<Identity.Base.Organizations.Abstractions.IOrganizationService>();
        var memberService = sp.GetRequiredService<Identity.Base.Organizations.Abstractions.IOrganizationMembershipService>();
        var userManager = sp.GetRequiredService<UserManager<Identity.Base.Identity.ApplicationUser>>();
        var seed = sp.GetRequiredService<IOptions<Identity.Base.Options.IdentitySeedOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(seed.Email))
        {
            var user = await userManager.FindByEmailAsync(seed.Email);
            if (user is not null)
            {
                var org = await orgService.CreateAsync(new Identity.Base.Organizations.Abstractions.OrganizationCreateRequest
                {
                    Slug = "acme",
                    DisplayName = "Acme Corp"
                }, ct);

                await memberService.AddMemberAsync(new Identity.Base.Organizations.Abstractions.OrganizationMembershipRequest
                {
                    OrganizationId = org.Id,
                    UserId = user.Id,
                    IsPrimary = true
                }, ct);
            }
        }
    });
    ```
  - Insert before `await app.RunAsync();`:
    ```csharp
    app.MapIdentityBaseOrganizationEndpoints();
    ```

- path: Identity.Base.Host/appsettings.Development.json
  - Ensure sections exist/updated:
    ```json
    {
      "IdentitySeed": {
        "Enabled": true,
        "Email": "admin@example.com",
        "Password": "P@ssword12345!",
        "Roles": ["IdentityAdmin"]
      },
      "Permissions": {
        "Definitions": [
          { "Name": "admin.organizations.read", "Description": "List and view organizations" },
          { "Name": "admin.organizations.manage", "Description": "Create, update, archive organizations" },
          { "Name": "admin.organizations.members.read", "Description": "List members" },
          { "Name": "admin.organizations.members.manage", "Description": "Manage memberships" },
          { "Name": "admin.organizations.roles.read", "Description": "View organization roles" },
          { "Name": "admin.organizations.roles.manage", "Description": "Manage organization roles and permissions" }
        ]
      },
      "Roles": {
        "Definitions": [
          {
            "Name": "IdentityAdmin",
            "Description": "Full administrative access",
            "Permissions": [
              "users.read", "users.create", "users.update", "users.lock",
              "users.reset-password", "users.reset-mfa", "users.manage-roles",
              "users.delete", "roles.read", "roles.manage",
              "admin.organizations.read", "admin.organizations.manage",
              "admin.organizations.members.read", "admin.organizations.members.manage",
              "admin.organizations.roles.read", "admin.organizations.roles.manage"
            ],
            "IsSystemRole": true
          }
        ]
      }
    }
    ```

# Configuration Snippets
Config: .env (Postgres)
```bash
CONNECTIONSTRINGS__PRIMARY=Host=localhost;Database=identity;Username=identity;Password=identity
```

# Verification
Command: dotnet run --project Identity.Base.Host
```bash
dotnet run --project Identity.Base.Host
```
Expect: Host starts, migrations apply, seeds run. Log contains "Seed user admin@example.com created successfully" and organization creation messages.

Command: Acquire access token via password grant
```bash
ACCESS_TOKEN=$(curl -s -X POST http://localhost:8080/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d 'grant_type=password&username=admin@example.com&password=P@ssword12345!&client_id=spa-client&scope=openid profile email offline_access identity.api identity.admin' | jq -r .access_token)
echo ${ACCESS_TOKEN} | head -c 20 && echo "..."
```
Expect: Non-empty token printed.

Command: List organizations (should include Acme Corp)
```bash
curl -s http://localhost:8080/organizations -H "Authorization: Bearer $ACCESS_TOKEN" | jq '.[0] | {id, slug, displayName}'
```
Expect: `{ "slug": "acme", "displayName": "Acme Corp" }` with a valid `id`.

Command: Verify membership for current user (via organizations list/me endpoints if exposed)
```bash
curl -s http://localhost:8080/users/me/organizations -H "Authorization: Bearer $ACCESS_TOKEN" | jq '.[0] | {organization: .organization.displayName, isPrimary}'
```
Expect: Shows membership for "Acme Corp" and `isPrimary: true`.

# Diagram
```mermaid
flowchart LR
  A[IdentitySeed] --> U[Admin User]
  subgraph Organizations
    ORGSEED[Org Role Seeder] --> ORoles[Org Roles]
  end
  C[AfterIdentitySeed Callback] --> O[Create Organization]
  C --> M[Add Admin Membership]
  U -->|password grant| T[Access Token]
  T --> API[Org/Admin APIs]
  API --> V[List Orgs]
```

# Outputs
- Admin user email/password configured and created.
- Organization "Acme Corp" created; admin membership set as primary.

# Completion Checklist
- [ ] Host compiles and starts without errors.
- [ ] Admin user created and able to obtain access token.
- [ ] `/organizations` returns the seeded organization.
- [ ] `/users/me/organizations` reflects primary membership.
