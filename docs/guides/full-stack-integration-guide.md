# Full Stack Integration Guide (.NET 9 + React 19)

This guide describes how to bootstrap the recommended Identity Base architecture: a dedicated Identity Host responsible for every authentication concern, a fleet of ASP.NET Core microservices that expose protected APIs, and a React 19 SPA that drives end-user experiences. Follow the steps in order; each section builds on the previous one.

---

## 1. Prerequisites

Install or confirm the following tooling:

| Tool | Recommended Version | Notes |
| --- | --- | --- |
| .NET SDK | 9.0.x | Required for the host and CLI tooling |
| node.js | 20.x | React 19 relies on the latest LTS features |
| npm | 10.x | Ships with node 20 |
| PostgreSQL | 16.x | Backing store for Identity Base packages |
| MailJet account | Optional locally, required for real email delivery |
| Git | Latest | Manages solution assets and environment files |

> For local development you can stub MailJet with MailHog and disable SMS MFA. Production deployments must supply real credentials.

---

## 2. Lay Out the Solution

```bash
mkdir identity-full-stack
cd identity-full-stack

dotnet new sln -n IdentityFullStack
dotnet new web -n IdentityHost
mkdir Services

dotnet sln add IdentityHost/IdentityHost.csproj
```

The `IdentityHost` project centralizes Identity Base. Each API surface lives in its own ASP.NET Core project under `Services/` (for example `Services/OrdersApi`). Add them to the solution as you create them.

---

## 3. Wire in Identity Base Packages

### 3.1 Install NuGet Packages

```bash
cd IdentityHost
dotnet add package Identity.Base
dotnet add package Identity.Base.Admin
dotnet add package Identity.Base.Organisations
dotnet add package Identity.Base.Email.MailJet # optional Mailjet sender
```

- `Identity.Base` provides the core identity, OpenIddict, MFA, and email flows.
- `Identity.Base.Admin` layers admin authorization and endpoints on top of RBAC (it implicitly registers `Identity.Base.Roles`).
- `Identity.Base.Organisations` adds organisation, membership, and organisation-scoped role management.

### 3.2 Replace `Program.cs`

```csharp
using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Email.MailJet;
using Identity.Base.Extensions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Endpoints;
using Identity.Base.Organisations.Extensions;
using Identity.Base.Roles;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Core identity surface (Identity, OpenIddict, MFA, external providers)
var identityBuilder = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
identityBuilder.UseMailJetEmailSender(); // optional Mailjet integration

// Admin API (includes Identity.Base.Roles registration)
var adminBuilder = builder.Services.AddIdentityAdmin(builder.Configuration);
adminBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
});

// Organisations (multi-tenant entities, memberships, organisation roles)
var organisationsBuilder = builder.Services.AddIdentityBaseOrganisations(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString);
});

// Optional: extend organisation model or seeding pipeline here
// organisationsBuilder.ConfigureOrganisationModel(modelBuilder => { ... });
// organisationsBuilder.AfterOrganisationSeed(async (sp, ct) => { ... });

var app = builder.Build();

// Automatically apply pending migrations and seed data on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;

    var identityContext = services.GetRequiredService<Identity.Base.Identity.AppDbContext>();
    await identityContext.Database.MigrateAsync();

    var rolesContext = services.GetService<IdentityRolesDbContext>();
    if (rolesContext is not null)
    {
        await rolesContext.Database.MigrateAsync();
        await services.SeedIdentityRolesAsync();
    }

    var organisationContext = services.GetService<OrganisationDbContext>();
    if (organisationContext is not null)
    {
        await organisationContext.Database.MigrateAsync();
    }
}

app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging()); // Plug in your preferred request logging
app.MapControllers();                   // Allow MVC controllers if you add any
app.MapApiEndpoints();                  // Core Identity Base endpoints
app.MapIdentityRolesUserEndpoints();    // GET /users/me/permissions and scope helpers
app.MapIdentityAdminEndpoints();        // /admin/users, /admin/roles
app.MapIdentityBaseOrganisationEndpoints(); // /organisations + membership management
app.MapHealthChecks("/healthz");

await app.RunAsync();
```

### 3.3 Supply Configuration (`appsettings.json`)

Populate the generated `appsettings.json` with the minimal sections shown below. Adjust values to match your environment and secrets store.

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
    "ConfirmationUrlTemplate": "https://localhost:5001/account/confirm?token={token}&userId={userId}",
    "PasswordResetUrlTemplate": "https://localhost:5001/reset-password?token={token}&userId={userId}",
    "ProfileFields": [
      { "Name": "displayName", "DisplayName": "Display Name", "Required": true, "MaxLength": 128 }
    ]
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
  "Mfa": {
    "Issuer": "Identity Base Sample",
    "Email": { "Enabled": true },
    "Sms": { "Enabled": false }
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:5173",
      "http://localhost:5173"
    ]
  },
  "ExternalProviders": {
    "Google": { "Enabled": false },
    "Microsoft": { "Enabled": false },
    "Apple": { "Enabled": false }
  },
  "OpenIddict": {
    "ServerKeys": { "Provider": "Development" },
    "Scopes": [
      { "Name": "identity.api", "DisplayName": "Identity API" },
      { "Name": "identity.admin", "DisplayName": "Identity Admin" },
      { "Name": "organisations.manage", "DisplayName": "Organisation Management" }
    ],
    "Applications": [
      {
        "ClientId": "spa-client",
        "ClientType": "public",
        "DisplayName": "React SPA",
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
          "scopes:identity.api",
          "scopes:identity.admin",
          "scopes:organisations.manage"
        ],
        "Requirements": ["requirements:pkce"]
      }
    ]
  },
  "Permissions": {
    "Definitions": [
      { "Name": "users.read", "Description": "List and view users" },
      { "Name": "users.manage-roles", "Description": "Assign user roles" },
      { "Name": "organisations.read", "Description": "Read organisations" },
      { "Name": "organisations.manage", "Description": "Manage organisations and memberships" }
    ]
  },
  "Roles": {
    "Definitions": [
      {
        "Name": "StandardUser",
        "Description": "Baseline access",
        "Permissions": [],
        "IsSystemRole": false
      },
      {
        "Name": "IdentityAdmin",
        "Description": "Full platform administration",
        "Permissions": [
          "users.read",
          "users.manage-roles",
          "organisations.read",
          "organisations.manage"
        ],
        "IsSystemRole": true
      }
    ],
    "DefaultUserRoles": ["StandardUser"],
    "DefaultAdminRoles": ["IdentityAdmin"]
  }
}
```

- Ensure the `OpenIddict` client matches the React app redirect URL.
- Confirmation and password reset templates must include `{token}` and `{userId}` placeholders to match the email flows.
- Configure Mailjet secrets only if the Mailjet add-on is enabled; otherwise you can omit the section or leave `Enabled` false. Add MFA secrets to user secrets or environment variables in production.
- Expand the `Permissions`/`Roles` lists as you add downstream authorization requirements.

### 3.4 Prepare Database Schema

Identity Base, Identity Base Roles, and Identity Base Organisations all ship their migrations inside the packages. The startup routine in `Program.cs` calls `Database.MigrateAsync()` for each DbContext, so the Identity Host automatically creates or updates the schema every time it boots. There is no manual `dotnet ef database update` workflow.

Only generate migrations if you extend the supplied contexts with custom entities. In that case, run `dotnet ef migrations add ...` within your host project and the startup block will pick up those additional migrations too.

### 3.5 Run the Host

```bash
dotnet run
```

- `https://localhost:5001/healthz` should report healthy checks.
- `POST /auth/register`, `POST /auth/login`, `/admin/users`, and `/organisations` are now available.
- Sign in with the seeded admin account to exercise the admin and organisation surfaces.

---

## 4. Protect Microservices with `Identity.Base.AspNet`

Each microservice should remain focused on its own domain logic while trusting the Identity Host for authentication. Reference `Identity.Base.AspNet` to enforce JWT bearer authentication consistently.

```bash
cd ../Services
dotnet new webapi -n OrdersApi
dotnet add OrdersApi/OrdersApi.csproj package Identity.Base.AspNet
dotnet sln ../IdentityFullStack.sln add OrdersApi/OrdersApi.csproj
```

Minimal `Program.cs` for a microservice:

```csharp
using Identity.Base.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBaseAuthentication("https://localhost:5001", audience: "identity.api");

var app = builder.Build();

app.UseIdentityBaseRequestLogging(enableDetailedLogging: true); // optional
app.UseIdentityBaseAuthentication();

app.MapGet("/orders", () => new[] { "ORD-1001", "ORD-1002" })
   .RequireAuthorization(policy => policy.RequireScope("identity.api"));

app.Run();
```

Repeat for each service, choosing a scope (or set of scopes) that reflects the API’s responsibilities. Represent those scopes in the Identity Host `OpenIddict` configuration and expose them to requesting clients.

---

## 5. Create the React 19 Frontend

### 5.1 Scaffold the App

```bash
cd ..
npm create vite@latest identity-spa -- --template react-ts
cd identity-spa
npm install
```

### 5.2 Install Identity Base Packages

```bash
npm install @identity-base/react-client @identity-base/react-organisations
```

Both packages declare React 18/19 peer dependencies, so your Vite app must use React 19 (`"react": "^19.0.0"` in `package.json`).

### 5.3 Configure Environment Variables

Create `.env.local`:

```env
VITE_IDENTITY_API_BASE=https://localhost:5001
VITE_IDENTITY_CLIENT_ID=spa-client
VITE_IDENTITY_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_IDENTITY_SCOPE="openid profile email offline_access identity.api identity.admin organisations.manage"
VITE_IDENTITY_LOG_LEVEL=debug
```

### 5.4 Bootstrap Identity & Organisation Providers

`src/config.ts`:

```ts
export const identityConfig = {
  apiBase: import.meta.env.VITE_IDENTITY_API_BASE ?? 'https://localhost:5001',
  clientId: import.meta.env.VITE_IDENTITY_CLIENT_ID ?? 'spa-client',
  redirectUri: import.meta.env.VITE_IDENTITY_REDIRECT_URI ?? 'http://localhost:5173/auth/callback',
  scope: import.meta.env.VITE_IDENTITY_SCOPE ?? 'openid profile identity.api',
  tokenStorage: 'localStorage' as const,
};
```

`src/main.tsx`:

```tsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { IdentityProvider } from '@identity-base/react-client';
import { OrganisationsProvider } from '@identity-base/react-organisations';
import App from './App';
import { identityConfig } from './config';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <IdentityProvider config={identityConfig}>
      <OrganisationsProvider apiBase={identityConfig.apiBase}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </OrganisationsProvider>
    </IdentityProvider>
  </React.StrictMode>,
);
```

`OrganisationsProvider` consumes the user’s tokens from `@identity-base/react-client`, loads the caller’s organisation memberships, and exposes helper hooks for switching organisations or managing memberships. Provide `apiBase` whenever the SPA’s origin differs from the Identity Host.

### 5.5 Organisation Hooks in Practice

Use the exported hooks to access membership data, list organisation members, and switch the active organisation context.

```tsx
import { useOrganisations, useOrganisationMembers, useOrganisationSwitcher } from '@identity-base/react-organisations';

export function OrganisationDashboard() {
  const { memberships, activeOrganisationId, switchActiveOrganisation, isLoadingOrganisations } = useOrganisations();
  const { members, isLoading: isLoadingMembers } = useOrganisationMembers(activeOrganisationId ?? undefined);
  const { isSwitching } = useOrganisationSwitcher();

  if (isLoadingOrganisations) return <p>Loading organisations…</p>;
  if (!activeOrganisationId) return <p>Select an organisation to continue.</p>;

  return (
    <>
      <select
        value={activeOrganisationId}
        onChange={(event) => switchActiveOrganisation(event.target.value)}
        disabled={isSwitching}
      >
        {memberships.map((membership) => (
          <option key={membership.organisationId} value={membership.organisationId}>
            {membership.organisationId}
          </option>
        ))}
      </select>

      {isLoadingMembers ? (
        <p>Loading members…</p>
      ) : (
        <ul>
          {members.map((member) => (
            <li key={member.userId}>{member.displayName ?? member.email ?? member.userId}</li>
          ))}
        </ul>
      )}
    </>
  );
}
```

The provider automatically refreshes organisation data when the user signs in or switches organisations and persists the active organisation in `localStorage`. Downstream API calls can include the active organisation ID in headers or query parameters if required by your microservices.

### 5.6 Implement Core Screens

| Screen | Hooks & APIs | Notes |
| --- | --- | --- |
| Registration | `useRegister`, `authManager.getProfileSchema()` | Render dynamic metadata fields defined in `Registration:ProfileFields`. |
| Login + MFA | `useLogin`, `useMfa()` | Handle `requiresTwoFactor`, drive challenge + verification steps. |
| Forgot / Reset Password | `useForgotPassword`, `useResetPassword` | Parse `token` & `userId` query params during reset. |
| Profile | `useProfile()` | Allow metadata updates via `authManager.updateProfile`. |
| Organisation Management | `useOrganisationList`, `useOrganisationMembers` from `@identity-base/react-organisations` | Surface CRUD and membership flows aligned with your permissions. |
| Admin User Management | Direct calls to `/admin/users` (fetch, assign roles) | Include admin-only UI guards by checking `authManager.hasPermission('users.manage-roles')`. |
| Domain APIs | Fetch from microservices such as `/orders`, `/inventory` | Attach access tokens from the React client (hooks expose `getAccessToken`). |

Leverage the hooks to centralize token exchange, refresh, and error handling. The React client automatically stores and rotates tokens using the chosen storage strategy.

Example call into a microservice using the access token issued by Identity Host:

```tsx
import { useIdentityContext } from '@identity-base/react-client';

export function OrdersList() {
  const { authManager } = useIdentityContext();

  const loadOrders = async () => {
    const token = await authManager?.getAccessToken();
    const response = await fetch('https://localhost:7001/orders', {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    return await response.json();
  };

  // invoke loadOrders inside a hook such as useQuery / useEffect
}
```

### 5.7 Route Guards and Permission Checks

```tsx
import { useAuthorization } from '@identity-base/react-client';

export function RequirePermission({ permission, children }: { permission: string; children: React.ReactNode }) {
  const { hasPermission, isLoading } = useAuthorization();
  if (isLoading) return <p>Loading…</p>;
  if (!hasPermission(permission)) return <p>Access denied.</p>;
  return <>{children}</>;
}
```

Use `RequirePermission` around admin and organisation components to enforce RBAC consistently with the backend.

### 5.8 Run the SPA

```bash
npm run dev
```

Vite serves the app on `http://localhost:5173` by default. Proxy protected API routes to the Identity host (edit `vite.config.ts` as needed).

---

## 6. Full Stack Smoke Test

1. Start PostgreSQL and ensure the configured database exists.
2. In `IdentityHost`, run `dotnet run`. Verify `GET /healthz` returns `Healthy`.
3. In `identity-spa`, run `npm run dev`. Confirm the React app loads at `http://localhost:5173`.
4. Register a new user; complete login and MFA if enabled.
5. Sign in with the seeded admin account (`admin@example.com`), navigate to admin pages, and verify role assignment + organisation management endpoints respond.
6. Hit each protected microservice endpoint (for example `/orders`) to validate `Identity.Base.AspNet` authentication and scope enforcement.

---

## 7. Next Steps

- Move secrets to [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or your vault provider before deploying.
- Add production-grade HTTPS certificates and switch `ServerKeys.Provider` away from `Development`.
- Expand automated tests (`dotnet test IdentityFullStack.sln`) once you add custom logic.
- Review `docs/reference/Engineering_Principles.md` to align new code with repository standards.

With the host and SPA configured, you now have an end-to-end stack that exercises the full Identity Base package family. Iterate on UI, extend permissions, and integrate your domain-specific APIs underneath the same authentication umbrella.
