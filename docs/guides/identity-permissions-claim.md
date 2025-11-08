# Ensuring the `identity.permissions` Claim Is Populated

This guide explains how to configure an ASP.NET Core host so that authenticated users receive the `identity.permissions` claim emitted by the Identity Base RBAC package. The claim is required by the admin endpoints and any downstream APIs that enforce fine-grained permissions.

## 1. Required Packages

Install the following NuGet packages:

| Package | Purpose |
| --- | --- |
| `Identity.Base` | Core identity + OpenIddict services. |
| `Identity.Base.Roles` | Permission catalog, role seeding, and the claims augmentor that issues `identity.permissions`. |
| `Identity.Base.Admin` (optional) | Admin APIs that consume the permissions claim. |
| `Identity.Base.AspNet` (optional) | JWT bearer helpers for downstream APIs. |

## 2. Configure `Program.cs`

```csharp
using Identity.Base.Extensions;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configureDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Primary")
        ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

    options.UseNpgsql(connectionString);
});

var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment, configureDbContext: configureDbContext);

builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext)
    .UseTablePrefix("Contoso"); // optional

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints(); // optional helper endpoint for debugging permissions

await app.RunAsync();
```

Key points:
- Call `AddIdentityRoles` **after** `AddIdentityBase` so the claims augmentor is registered.
- Provide a persistent store (`IdentityRolesDbContext`) so role assignments and permissions can be resolved at runtime.
- Map `MapIdentityRolesUserEndpoints()` if you want to expose `GET /users/me/permissions` for debugging.
- Use `UseTablePrefix("<Prefix>")` if you need the RBAC tables to align with a custom prefix (default: `Identity_`).

## 3. Seed Permissions and Roles

The RBAC package reads configuration from the `Permissions` and `Roles` sections of `appsettings.json`. Example:

```json
"Permissions": {
  "Definitions": [
    { "Name": "users.read", "Description": "View users" },
    { "Name": "users.manage", "Description": "Manage users" },
    { "Name": "admin.organizations.manage", "Description": "Manage organizations system-wide" }
  ]
},
"Roles": {
  "Definitions": [
    {
      "Name": "SupportAgent",
      "Description": "Read-only access",
      "Permissions": ["users.read"],
      "IsSystemRole": false
    },
    {
      "Name": "Administrator",
      "Description": "Full access",
      "Permissions": ["users.read", "users.manage", "admin.organizations.manage"],
      "IsSystemRole": true
    }
  ],
  "DefaultUserRoles": ["SupportAgent"],
  "DefaultAdminRoles": ["Administrator"]
}
```

At startup (or as part of deployment), run the seeding helpers:

```csharp
await app.Services.SeedIdentityRolesAsync();
```

This creates the role definitions and default assignments so the permission resolver can build the user’s effective permission set.

> Organization roles (e.g., `OrgOwner`) now receive the user-scoped (`user.organizations.*`) permissions. Give `admin.organizations.*` to a dedicated admin role if you need platform-wide access.

The organizations package also emits an `org:memberships` claim containing all organization IDs for the caller. Combine it with the middleware:

```csharp
app.UseOrganizationContextFromHeader();
```

and the client-side `X-Organization-Id` header to indicate which organization is active per request. The middleware ignores the header when the request targets `/organizations…` (admin APIs) so global admins remain unrestricted. When memberships change, refresh tokens so the `org:memberships` claim stays current.

## 4. Let Startup Hosted Services Apply Migrations

Identity Base no longer runs EF Core migrations for you. Generate/apply migrations in your host (for both `AppDbContext` and the RBAC context) before the application starts, then call `SeedIdentityRolesAsync()` to synchronize roles/permissions from configuration.

Nothing else is required: ensure both DbContexts point at databases where the app has migrate permissions, and the hosted services will run before the HTTP pipeline starts handling traffic. For containerised or server environments, keep the health probes delayed until startup migrations complete.

## 5. Refresh Sign-In After Role Changes

Whenever you change a user’s roles or permissions, refresh their sign-in so the claims augmentor runs again:

```csharp
await signInManager.RefreshSignInAsync(user);
```

For SPA flows, request a new token via `IdentityAuthManager.refreshTokens()` (React client) to pick up the updated claim.

## 6. Verify the Claim

Inspect the authenticated principal (cookie or JWT) and confirm a claim exists with type `identity.permissions`. For example, the `GET /users/me/permissions` endpoint (from `MapIdentityRolesUserEndpoints`) returns the effective permission set as seen by the resolver.

## 7. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| No `identity.permissions` claim | `AddIdentityRoles` not called or registrations missing | Ensure the roles package is added and the roles DbContext is configured. |
| Claim is empty | No seeded permissions or user lacks role assignments | Seed permissions/roles and assign at least one role. |
| Claim missing after role change | Sign-in cookie/JWT not refreshed | Call `RefreshSignInAsync` or reissue tokens. |
| Admin API still returns 403 | Required scope not present | Check `AdminApiOptions.RequiredScope` and include it in the token. |

With these steps, every authenticated user with roles will receive the `identity.permissions` claim, and admin endpoints (`RequireAdminPermission(...)`) will succeed when the user holds the necessary permission.
