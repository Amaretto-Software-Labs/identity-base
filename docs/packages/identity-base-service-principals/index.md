# Identity.Base.ServicePrincipals

## Overview

`Identity.Base.ServicePrincipals` adds managed machine identities to an Identity Base host. A service principal has an immutable `Guid` subject and generated `client_id`, one or more independently revocable credentials, and assignments to the same roles and permission catalog used for users. It authenticates through the OAuth 2.0 `client_credentials` grant and receives short-lived access tokens without refresh tokens.

The package is opt-in. It depends on `Identity.Base`, `Identity.Base.Roles`, and `Identity.Base.Admin`; it does not replace configuration-seeded OpenIddict clients. Existing clients with `AllowClientCredentialsFlow` continue to use their standard OpenIddict secret.

## Installation and Wiring

```bash
dotnet add package Identity.Base.ServicePrincipals
```

Register the core and admin/RBAC packages first, then the service-principal package with its own DbContext configuration:

```csharp
using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Extensions;
using Identity.Base.Roles.Endpoints;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Extensions;
using Microsoft.EntityFrameworkCore;

var configureDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("Primary")
        ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

    options.UseNpgsql(connectionString); // or UseSqlServer(connectionString)
});

builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: configureDbContext);

// Includes Identity.Base.Roles and registers IdentityRolesDbContext.
builder.Services.AddIdentityAdmin(builder.Configuration, configureDbContext);

builder.Services.AddIdentityBaseServicePrincipals(
    builder.Configuration,
    configureDbContext);

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseServicePrincipalEndpoints();
```

Use `ConfigureModel(...)` on the returned `IdentityBaseServicePrincipalsBuilder` for host-specific EF model changes. Use `UseDbContext<TContext>()` when a derived `ServicePrincipalDbContext` has already been registered.

## Configuration

The package binds `Identity:ServicePrincipals`:

```json
{
  "Identity": {
    "ServicePrincipals": {
      "AccessTokenLifetime": "00:15:00",
      "AllowedScopes": ["identity.api"]
    }
  }
}
```

| Setting | Default | Purpose |
| --- | --- | --- |
| `AccessTokenLifetime` | `00:15:00` | Lifetime applied to managed service-principal access tokens. Must be positive. |
| `AllowedScopes` | `["identity.api"]` | Scopes granted to newly created managed OpenIddict applications. Every value must also exist under `OpenIddict:Scopes`. |

Changing `AllowedScopes` affects managed applications created after the change. Existing OpenIddict application permissions are not rewritten automatically.

The admin endpoints use the normal `IdentityAdmin:RequiredScope` setting (default `identity.admin`) in addition to their permission requirement. Grant the following permissions to the operator roles that need them:

- `service-principals.read`
- `service-principals.create`
- `service-principals.update`
- `service-principals.disable`
- `service-principals.manage-roles`
- `service-principals.manage-credentials`

The package contributes these entries to the shared permission catalog automatically. Role definitions still decide which administrators receive them.

## Database and Migrations

Two host-owned schema changes are required:

1. `ServicePrincipalDbContext` stores `ServicePrincipals` and `ServicePrincipalCredentials`.
2. `IdentityRolesDbContext` stores the new `ServicePrincipalRoles` join table.

Generate and apply both migrations from the consuming host:

```bash
dotnet ef migrations add AddServicePrincipals \
  --project IdentityHost/IdentityHost.csproj \
  --startup-project IdentityHost/IdentityHost.csproj \
  --context Identity.Base.ServicePrincipals.Data.ServicePrincipalDbContext \
  --output-dir Data/Migrations/ServicePrincipals

dotnet ef migrations add AddServicePrincipalRoles \
  --project IdentityHost/IdentityHost.csproj \
  --startup-project IdentityHost/IdentityHost.csproj \
  --context Identity.Base.Roles.IdentityRolesDbContext \
  --output-dir Data/Migrations/Roles

dotnet ef database update \
  --project IdentityHost/IdentityHost.csproj \
  --startup-project IdentityHost/IdentityHost.csproj \
  --context Identity.Base.ServicePrincipals.Data.ServicePrincipalDbContext

dotnet ef database update \
  --project IdentityHost/IdentityHost.csproj \
  --startup-project IdentityHost/IdentityHost.csproj \
  --context Identity.Base.Roles.IdentityRolesDbContext
```

Apply migrations before mapping the feature in a deployed environment. The repository’s `Identity.Base.Host.PostgreSqlMigrations` and `Identity.Base.Host.SqlServerMigrations` projects show provider-specific examples.

## Admin API

All routes are beneath `/admin/service-principals`.

| Method and route | Purpose | Permission |
| --- | --- | --- |
| `GET /admin/service-principals` | Paged list; supports `page`, `pageSize`, `search`, and `disabled`. | `service-principals.read` |
| `GET /admin/service-principals/{id}` | Read one principal and its role names. | `service-principals.read` |
| `POST /admin/service-principals` | Create from `{ "displayName": "..." }`; returns the generated immutable client ID. | `service-principals.create` |
| `PUT /admin/service-principals/{id}` | Update the display name using the current `concurrencyStamp`. | `service-principals.update` |
| `POST /admin/service-principals/{id}/disable` | Disable, revoke every credential, and revoke token entries. Optional `{ "reason": "..." }`. | `service-principals.disable` |
| `POST /admin/service-principals/{id}/restore` | Re-enable the principal. Credentials remain revoked. | `service-principals.disable` |
| `GET /admin/service-principals/{id}/roles` | Read assigned global role names. | `service-principals.read` |
| `PUT /admin/service-principals/{id}/roles` | Replace assignments using `{ "roles": ["Worker"] }`. | `service-principals.manage-roles` |
| `GET /admin/service-principals/{id}/credentials` | List credential metadata; secrets are never returned. | `service-principals.read` |
| `POST /admin/service-principals/{id}/credentials` | Issue a credential using `name` and optional future `expiresAt`. | `service-principals.manage-credentials` |
| `POST /admin/service-principals/{id}/credentials/{credentialId}/revoke` | Revoke one credential with an optional reason. | `service-principals.manage-credentials` |
| `POST /admin/service-principals/{id}/credentials/revoke-all` | Revoke every credential and token entry. | `service-principals.manage-credentials` |

`pageSize` is clamped to 1–100. Updates use optimistic concurrency and return `409 Conflict` if the supplied stamp is stale. Client IDs cannot be edited.

The issue-credential response is the only response that contains the plaintext secret:

```json
{
  "id": "b6f10cc0-1f24-4caf-84ba-18be128ccb28",
  "name": "production",
  "secret": "one-time-value",
  "createdAt": "2026-07-26T12:00:00Z",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

Copy it directly to an approved secret store. Identity Base persists only an ASP.NET Identity password hash and cannot recover the secret later.

## Obtain a Token

Use HTTP Basic authentication or form fields at the standard token endpoint:

```bash
curl -sS https://identity.example.com/connect/token \
  -u "$CLIENT_ID:$CLIENT_SECRET" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'scope=identity.api'
```

A managed token uses the service principal’s `Guid` as `sub` and includes:

- `client_id`
- the display name
- `identity.principal_type=ServicePrincipal`
- `identity.permissions` containing the union of permissions assigned through global roles

Role changes affect newly issued tokens. They do not rewrite claims in an already issued token. Disabling a principal or using revoke-all revokes its OpenIddict token entries, and the package enables token-entry validation inside the Identity host. A downstream API performing only offline validation of a self-contained JWT cannot observe that database state before `exp`; use the short token lifetime or an online validation strategy where immediate downstream revocation is required. Revoking only one credential prevents future authentication with that secret but does not revoke tokens already obtained with it.

## TypeScript Administration

`@identity-base/client-core` and packages that re-export it expose the complete surface through `IdentityAuthManager.admin.servicePrincipals`:

```ts
const principal = await authManager.admin.servicePrincipals.create({
  displayName: 'Invoice Worker',
})

await authManager.admin.servicePrincipals.updateRoles(principal.id, ['InvoiceWorker'])

const issued = await authManager.admin.servicePrincipals.issueCredential(principal.id, {
  name: 'production',
  expiresAt: '2027-01-01T00:00:00Z',
})

// Persist issued.secret now; it cannot be retrieved later.
```

The namespace also provides `list`, `get`, `update`, `disable`, `restore`, `getRoles`, `listCredentials`, `revokeCredential`, and `revokeAllCredentials`.

## Lifecycle and Extension Points

Register one or more `IServicePrincipalLifecycleListener` implementations to run product-specific governance before disable:

```csharp
builder.Services.AddScoped<IServicePrincipalLifecycleListener, WorkloadCleanupListener>();
```

Throw `InvalidOperationException` from `BeforeDisableAsync` to reject the operation with `409 Conflict`. Other exceptions are treated as unexpected failures. The listener runs before credentials and tokens are revoked.

The core package also exposes `IClientCredentialsPrincipalProvider` and `IManagedClientCredentialsClientResolver` for alternative managed-client sources. The service-principal package supplies implementations for its database-backed identities.

## Operational Guidance

- Issue separate credentials for each deployment or workload so rotation and incident response can target one consumer.
- Prefer expirations and overlap old/new credentials only for the duration of a planned rotation.
- Assign least-privilege roles; service principals use the same permission catalog as users but never receive user or organization membership implicitly.
- Use revoke-all or disable when existing access tokens must stop working. Use single-credential revoke for routine rotation after a replacement is live.
- Restoring a principal never restores a credential. Issue a new one explicitly.
- Audit events are emitted for create, update, disable/restore, role changes, and credential issue/revocation operations. Connect `IAuditLogger` to durable observability storage.

## Troubleshooting

- **`unauthorized_client`** – verify the client ID belongs to an enabled managed principal and request only scopes granted when it was created.
- **`invalid_client`** – the secret is incorrect, expired, revoked, or belongs to a disabled principal.
- **403 from admin routes** – the operator token needs the configured admin scope and the route’s `service-principals.*` permission.
- **Missing permissions in a machine token** – assign an existing role, request a new token, and verify that role has permission rows in `IdentityRolesDbContext`.
- **Missing tables** – regenerate and apply migrations for both contexts; adding only `ServicePrincipalDbContext` is insufficient for role assignments.

## Related Documentation

- [Getting Started](../../guides/getting-started.md)
- [Admin Operations Guide](../../guides/admin-operations-guide.md)
- [Package Architecture](../../reference/identity-base-package-architecture.md)
- [Database Design and Migration Guidelines](../../reference/Database_Design_Guidelines.md)
- [HTTP API Reference](../../reference/http-api.md)
