# Getting Started with Identity Base Packages

This walkthrough shows how to stand up Identity Base as a service using only the published NuGet packages. By the end of each stage you will have a working host that you can run and test. Subsequent sections add optional RBAC (`Identity.Base.Roles`) and the full admin API (`Identity.Base.Admin`).

> Repository: [Identity Base](https://github.com/Amaretto-Software-Labs/identity-base) • Issue tracker: [GitHub Issues](https://github.com/Amaretto-Software-Labs/identity-base/issues)

---

## 1. Create the Host Project

1. Create an empty ASP.NET Core project:
   ```bash
   dotnet new web -n IdentityHost
   cd IdentityHost
   ```
2. (Optional) Install the EF Core CLI if you have not already:
   ```bash
   dotnet tool install --global dotnet-ef
   ```

---

## 2. Install Identity Base Core

Refer to [docs/packages/identity-base/index.md](../packages/identity-base/index.md) for a full overview of the core package’s endpoints, extension points, and configuration knobs.

### 2.1 Add NuGet Packages
```bash
dotnet add package Identity.Base
dotnet add package Identity.Base.Email.MailJet # optional Mailjet sender (see docs/packages/identity-base-email-mailjet/index.md)
dotnet add package Identity.Base.Email.SendGrid # optional SendGrid sender (see docs/packages/identity-base-email-sendgrid/index.md)
```

### 2.2 Configure `Program.cs`
Replace the generated file with the following minimal host:
```csharp
using Identity.Base.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configureDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Primary")
        ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

    options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
    // or options.UseSqlServer(connectionString);
});

// Registers Identity Base services, including AppDbContext, Identity, OpenIddict, MFA, etc.
var identity = builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: configureDbContext);

identity.UseTablePrefix("Contoso");   // optional: override the default Identity_ prefix
// Optional: enable email delivery if an add-on package is installed (choose one)
identity.UseMailJetEmailSender();
// identity.UseSendGridEmailSender();

var app = builder.Build();

app.UseApiPipeline();         // HTTPS redirection, CORS, auth, etc.
app.MapControllers();         // enables controller discovery if you add any
app.MapApiEndpoints();        // Identity Base authentication/profile endpoints

await app.RunAsync();
```

> Need structured request logging? Pass your own middleware: `app.UseApiPipeline(app => app.UseSerilogRequestLogging());` or plug in your preferred logging framework.

### 2.3 Provide Configuration
Add an `appsettings.json` (or edit the existing file) with at least the following sections. Adjust values to match your environment:
```json
{
  "ConnectionStrings": {
    "Primary": "User ID=postgres;Password=P@ssword123;Host=localhost;Port=5432;Database=identity;Include Error Detail=true"
  },
  "IdentitySeed": {
    "Enabled": true,
    "Email": "admin@example.com",
    "Password": "Passw0rd!Passw0rd!",
    "Roles": ["IdentityAdmin"]
  },
  "Registration": {
    "ConfirmationUrlTemplate": "http://localhost:5173/auth/confirm?token={token}&userId={userId}",
    "PasswordResetUrlTemplate": "http://localhost:5173/reset-password?token={token}&userId={userId}",
    "ProfileFields": [
      { "Name": "displayName", "DisplayName": "Display Name", "Required": true, "MaxLength": 128 }
    ]
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:5173", "http://localhost:5173"]
  },
  "MailJet": {
    "Enabled": true,
    "FromEmail": "noreply@example.com",
    "FromName": "Identity Base",
    "ApiKey": "your-mailjet-key",
    "ApiSecret": "your-mailjet-secret",
    "Templates": {
      "Confirmation": 123456,
      "PasswordReset": 234567,
      "MfaChallenge": 345678
    }
  },
  "SendGrid": {
    "Enabled": false,
    "FromEmail": "noreply@example.com",
    "FromName": "Identity Base",
    "ApiKey": "your-sendgrid-key",
    "Templates": {
      "Confirmation": "d-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "PasswordReset": "d-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
      "MfaChallenge": "d-cccccccccccccccccccccccccccccccc"
    }
  },
  "Mfa": {
    "Issuer": "Identity Base",
    "Email": { "Enabled": true },
    "Sms": {
      "Enabled": false,
      "FromPhoneNumber": "+15005550006",
      "AccountSid": "twilio-account-sid",
      "AuthToken": "twilio-auth-token"
    }
  },
  "ExternalProviders": {
    "Google": { "Enabled": false },
    "Microsoft": { "Enabled": false },
    "Apple": { "Enabled": false }
  },
  "OpenIddict": {
    "ServerKeys": { "Provider": "Development" },
    "Applications": [
      {
        "ClientId": "spa-client",
        "ClientType": "public",
        "RedirectUris": ["http://localhost:5173/auth/callback"],
        "Permissions": [
          "endpoints:authorization",
          "endpoints:token",
          "endpoints:userinfo",
          "grant_types:authorization_code",
          "response_types:code",
          "scopes:openid",
          "scopes:profile",
          "scopes:email",
          "scopes:offline_access",
          "scopes:identity.api"
        ],
        "Requirements": ["requirements:pkce"]
      }
    ],
    "Scopes": [
      {
        "Name": "identity.api",
        "DisplayName": "Identity API",
        "Resources": ["identity.api"]
      },
      {
        "Name": "identity.admin",
        "DisplayName": "Identity Admin",
        "Resources": ["identity.api"]
      }
    ]
  }
}
```
Key sections:
- `ConnectionStrings:Primary` – required for the internal `AppDbContext`.
- `IdentitySeed` – optionally bootstrap an admin user.
- `Registration` – confirmation and password reset URLs must include `{token}` **and** `{userId}` placeholders.
- `MailJet` / `SendGrid` (optional) – configure only when the corresponding package is referenced. Leave `Enabled` as `false` to skip sends.
- `Mfa`, `ExternalProviders` – supply credentials/enabled flags as needed.
- `OpenIddict` – register clients, scopes, and key management strategy.

### 2.3.1 Default OAuth scopes (and how to add them)

Identity Base uses a mix of:
- **OAuth scopes** (e.g., `openid`, `profile`, `email`, `offline_access`, `identity.api`, `identity.admin`) requested by clients during authorization.
- **Permission claims** (e.g., `users.read`, `admin.organizations.manage`) resolved from RBAC and emitted as `identity.permissions` for fine-grained authorization.

The two commonly needed custom scopes are:
- `identity.api` – a “default API scope” intended for your resource servers (microservices). Many samples and helpers assume this string.
- `identity.admin` – required by admin endpoints by default (see `IdentityAdmin:RequiredScope` in `Identity.Base.Admin` and `Organizations:Authorization:AdminRequiredScope` in `Identity.Base.Organizations`).

To enable a scope you must:
1. Define it under `OpenIddict:Scopes` (and set `Resources` so the access token gets the correct `aud` claim).
2. Grant it to a client by adding `scopes:<scopeName>` to that client’s `OpenIddict:Applications[].Permissions`.

> Note: clients only receive the scopes you explicitly grant via `OpenIddict:Applications[].Permissions` (e.g. `scopes:identity.api`, `scopes:identity.admin`). The built-in OpenIddict seeder no longer blanket-grants every configured scope to every client.

> Tip: if you disable scope checks for admin endpoints by setting `IdentityAdmin:RequiredScope` to `null`, clients no longer need `identity.admin` for the admin APIs (permissions still apply).

If you need the database objects to use a different prefix than `Identity_`, call `identity.UseTablePrefix("Contoso")` (and the corresponding `UseTablePrefix` helpers on RBAC/organization builders) before running migrations.

### 2.4 Apply the Core Migrations
Generate the database schema inside your host project (example below uses `Identity.Base.Host`; swap in your own host project if different):
```bash
dotnet ef migrations add InitialIdentityBase \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Data.AppDbContext \
  --output-dir Data/Migrations/IdentityBase

dotnet ef database update \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Data.AppDbContext
```

### 2.5 Run & Verify
```bash
dotnet run
```
Visit `https://localhost:5000/healthz` to confirm the service is up. If you enabled seeding, the bootstrap admin user is now available.

At this stage you have the complete identity, registration, MFA, and OAuth surface without RBAC or admin APIs.

### 2.6 Endpoint specs (OpenAPI)

Identity Base registers ASP.NET Core OpenAPI and (by default) serves it **only in Development**.

- OpenAPI JSON: `GET /openapi/v1.json`

If you need endpoint specs outside Development (not recommended for public deployments), map it explicitly in your host instead of relying on `UseApiPipeline()`’s Development-only mapping.

---

## 3. (Optional) Add Role-Based Access Control

If you need role management and effective permission resolution, add the `Identity.Base.Roles` package. The full package reference (services, options, endpoints) is documented at [docs/packages/identity-base-roles/index.md](../packages/identity-base-roles/index.md).

### 3.1 Install Package
```bash
dotnet add package Identity.Base.Roles
```

### 3.2 Register Services and Endpoints
Update `Program.cs` to register the roles services and expose the permissions endpoint:
```csharp
using Identity.Base.Roles;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;

// ... after AddIdentityBase
builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext)
    .UseTablePrefix("Contoso");

var app = builder.Build();

app.UseApiPipeline();
app.MapControllers();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();   // exposes GET /users/me/permissions

await app.RunAsync();
```

### 3.3 Supply Role Configuration
Add the `Permissions` and `Roles` sections to `appsettings.json` (adjust names as required):
```json
"Permissions": {
  "Definitions": [
    { "Name": "users.read", "Description": "View user directory" },
    { "Name": "users.manage-roles", "Description": "Assign roles" }
  ]
},
"Roles": {
  "Definitions": [
    {
      "Name": "StandardUser",
      "Description": "Baseline end user",
      "Permissions": [],
      "IsSystemRole": false
    },
    {
      "Name": "IdentityAdmin",
      "Description": "Full access",
      "Permissions": ["users.read", "users.manage-roles"],
      "IsSystemRole": true
    }
  ],
  "DefaultUserRoles": ["StandardUser"],
  "DefaultAdminRoles": ["IdentityAdmin"]
}
```

### 3.4 Create RBAC Migrations
Run the following commands from your host project (again using `Identity.Base.Host` as the sample):
```bash
dotnet ef migrations add InitialIdentityRoles \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Roles.Data.IdentityRolesDbContext \
  --output-dir Data/Migrations/IdentityRoles

dotnet ef database update \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Roles.Data.IdentityRolesDbContext
```

Re-run `dotnet run` and check `GET https://localhost:5000/users/me/permissions` after authenticating—your roles now determine the returned permission set.

---

## 4. (Optional) Add Organization Management

Install the organizations add-on if you need per-tenant organizations, memberships, and organization-level roles. The comprehensive package reference lives at [docs/packages/identity-base-organizations/index.md](../packages/identity-base-organizations/index.md).

### 5.1 Install Package
```bash
dotnet add package Identity.Base.Organizations
```

### 4.2 Register Services & Endpoints
Add the organizations registration after Identity Base (and, if present, RBAC) in `Program.cs`:
```csharp
using Identity.Base.Organizations.Extensions;
using Microsoft.EntityFrameworkCore;

var organizationsBuilder = builder.Services.AddIdentityBaseOrganizations(configureDbContext)
    .UseTablePrefix("Contoso");

organizationsBuilder.ConfigureOrganizationModel(modelBuilder =>
{
    // Optional: add custom indexes or shadow properties.
});

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
```

### 4.3 Apply Organization Migrations
Run the migrations from your host project so they target your chosen provider:
```bash
dotnet ef migrations add InitialOrganizations \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Organizations.Data.OrganizationDbContext \
  --output-dir Data/Migrations/Organizations

dotnet ef database update \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Organizations.Data.OrganizationDbContext
```

The hosted seed service will provision the default organization roles (`OrgOwner`, `OrgManager`, `OrgMember`) after migrations have run.

### 4.4 Extend Hooks
Use the builder hooks when you need custom behaviour:
```csharp
organizationsBuilder
    .AfterOrganizationSeed(async (sp, ct) => { /* custom seeding */ })
    .AddOrganizationScopeResolver<CustomScopeResolver>()
    .AddOrganizationClaimFormatter<CustomClaimFormatter>();
```

At this stage your host exposes organization CRUD, membership, and role endpoints alongside identity + RBAC features.


---

## 5. (Optional) Add the Admin API

`Identity.Base.Admin` layers admin endpoints on top of the roles package. If you add this package you do **not** need the separate `AddIdentityRoles` registration from the previous step (the admin builder already includes it). Consult the full package reference at [docs/packages/identity-base-admin/index.md](../packages/identity-base-admin/index.md) for endpoint details and configuration tips.

### 4.1 Install Package
```bash
dotnet add package Identity.Base.Admin
```

### 5.2 Update `Program.cs`
```csharp
using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;

// ... after AddIdentityBase
builder.Services.AddIdentityAdmin(builder.Configuration, configureDbContext)
    .UseTablePrefix("Contoso");

var app = builder.Build();

app.UseApiPipeline();
app.MapControllers();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();       // registers /admin/users and /admin/roles
app.MapIdentityRolesUserEndpoints();   // keep the permissions endpoint for guards

await app.RunAsync();
```

### 5.3 Configuration Checklist
- Ensure the `Permissions` and `Roles` sections include all admin permissions (see sample in section 3).
- Expand `OpenIddict:Applications` to include the `identity.admin` scope and expose it to admin clients.
- Update `IdentitySeed:Roles` or `Roles:DefaultAdminRoles` so at least one account receives the admin role.
- Keep `VITE_AUTHORIZE_SCOPE` (or equivalent client configuration) aligned with the new admin scope.

### 5.4 Database
If you have already applied the RBAC migrations, no additional migrations are required—the admin package shares `IdentityRolesDbContext`. Otherwise, follow step 3.4 before running the service.

### 5.5 Verify
After `dotnet run`, authenticate with an admin account and call `GET https://localhost:5000/admin/users` (expect `403` without the admin scope/permissions and `200` when authorized). The sample React client in `apps/sample-client` can now drive the full admin experience.

---

## 6. Where to Go Next
- Admin workflows: [`docs/guides/admin-operations-guide.md`](./admin-operations-guide.md)
- React harness & integration patterns: [`docs/guides/integration-guide.md`](./integration-guide.md)
- Raising issues or feature requests: [GitHub Issues](https://github.com/Amaretto-Software-Labs/identity-base/issues)
- AI Agent Contributor rules: [`AGENTS.md`](../../AGENTS.md)

With the packages wired into your host, you now have a configurable identity platform that can grow from core authentication to full-featured administrative tooling. EOF
