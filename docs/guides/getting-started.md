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

### 2.1 Add NuGet Packages
```bash
dotnet add package Identity.Base
```

### 2.2 Configure `Program.cs`
Replace the generated file with the following minimal host:
```csharp
using Identity.Base.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Registers Identity Base services, including AppDbContext, Identity, OpenIddict, mail, MFA, etc.
builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApiPipeline();         // HTTPS redirection, Serilog, CORS, auth, etc.
app.MapControllers();         // enables controller discovery if you add any
app.MapApiEndpoints();        // Identity Base authentication/profile endpoints

await app.RunAsync();
```

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
    "ConfirmationUrlTemplate": "https://localhost:5001/account/confirm?token={token}&email={email}",
    "PasswordResetUrlTemplate": "https://localhost:5001/reset-password?token={token}&email={email}",
    "ProfileFields": [
      { "Name": "displayName", "DisplayName": "Display Name", "Required": true, "MaxLength": 128 }
    ]
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:5173", "http://localhost:5173"]
  },
  "MailJet": {
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
    ]
  }
}
```
Key sections:
- `ConnectionStrings:Primary` – required for the internal `AppDbContext`.
- `IdentitySeed` – optionally bootstrap an admin user.
- `MailJet`, `Mfa`, `ExternalProviders` – supply credentials/enabled flags as needed.
- `OpenIddict` – register clients, scopes, and key management strategy.

### 2.4 Apply the Core Migrations
Generate the database schema inside your host project:
```bash
dotnet ef migrations add InitialIdentityBase \
  --context Identity.Base.Identity.AppDbContext \
  --output-dir Data/Migrations/IdentityBase

dotnet ef database update \
  --context Identity.Base.Identity.AppDbContext
```

### 2.5 Run & Verify
```bash
dotnet run
```
Visit `https://localhost:5000/healthz` to confirm the service is up. If you enabled seeding, the bootstrap admin user is now available.

At this stage you have the complete identity, registration, MFA, and OAuth surface without RBAC or admin APIs.

---

## 3. (Optional) Add Role-Based Access Control

If you need role management and effective permission resolution, add the `Identity.Base.Roles` package.

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
var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
});

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
```bash
dotnet ef migrations add InitialIdentityRoles \
  --context Identity.Base.Roles.IdentityRolesDbContext \
  --output-dir Data/Migrations/IdentityRoles

dotnet ef database update \
  --context Identity.Base.Roles.IdentityRolesDbContext
```

Re-run `dotnet run` and check `GET https://localhost:5000/users/me/permissions` after authenticating—your roles now determine the returned permission set.

---

## 4. (Optional) Add the Admin API

`Identity.Base.Admin` layers admin endpoints on top of the roles package. If you add this package you do **not** need the separate `AddIdentityRoles` registration from the previous step (the admin builder already includes it).

### 4.1 Install Package
```bash
dotnet add package Identity.Base.Admin
```

### 4.2 Update `Program.cs`
```csharp
using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Roles;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;

// ... after AddIdentityBase
var adminBuilder = builder.Services.AddIdentityAdmin(builder.Configuration);
adminBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
});

var app = builder.Build();

app.UseApiPipeline();
app.MapControllers();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();       // registers /admin/users and /admin/roles
app.MapIdentityRolesUserEndpoints();   // keep the permissions endpoint for guards

await app.RunAsync();
```

### 4.3 Configuration Checklist
- Ensure the `Permissions` and `Roles` sections include all admin permissions (see sample in section 3).
- Expand `OpenIddict:Applications` to include the `identity.admin` scope and expose it to admin clients.
- Update `IdentitySeed:Roles` or `Roles:DefaultAdminRoles` so at least one account receives the admin role.
- Keep `VITE_AUTHORIZE_SCOPE` (or equivalent client configuration) aligned with the new admin scope.

### 4.4 Database
If you have already applied the RBAC migrations, no additional migrations are required—the admin package shares `IdentityRolesDbContext`. Otherwise, follow step 3.4 before running the service.

### 4.5 Verify
After `dotnet run`, authenticate with an admin account and call `GET https://localhost:5000/admin/users` (expect `403` without the admin scope/permissions and `200` when authorized). The sample React client in `apps/sample-client` can now drive the full admin experience.

---

## 5. Where to Go Next
- Admin workflows: [`docs/guides/admin-operations-guide.md`](./admin-operations-guide.md)
- React harness & integration patterns: [`docs/guides/integration-guide.md`](./integration-guide.md)
- Raising issues or feature requests: [GitHub Issues](https://github.com/Amaretto-Software-Labs/identity-base/issues)
- AI Agent Contributor rules: [`AGENTS.md`](../../AGENTS.md)

With the packages wired into your host, you now have a configurable identity platform that can grow from core authentication to full-featured administrative tooling. EOF
