# Identity.Base.ServicePrincipals

> Full setup, endpoint, token, SDK, migration, and operational documentation lives at [docs/packages/identity-base-service-principals/index.md](../docs/packages/identity-base-service-principals/index.md).

Opt-in managed machine identities for Identity Base:

- immutable generated client IDs and `Guid` subjects;
- multiple independently revocable, one-time-returned credentials;
- global RBAC role assignments and `identity.permissions` claims;
- short-lived OAuth 2.0 `client_credentials` access tokens without refresh tokens;
- token-entry revocation on disable and revoke-all;
- permission-protected admin APIs and typed client-core contracts.

## Quick Start

```csharp
builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: configureDbContext);
builder.Services.AddIdentityAdmin(builder.Configuration, configureRolesDbContext);
builder.Services.AddIdentityBaseServicePrincipals(
    builder.Configuration,
    configureServicePrincipalDbContext);

var app = builder.Build();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
app.MapIdentityBaseServicePrincipalEndpoints();
```

The host owns migrations for both `ServicePrincipalDbContext` and the extended `IdentityRolesDbContext`. Configure defaults under `Identity:ServicePrincipals`:

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

Admin creation accepts a display name only. Identity Base generates the immutable `client_id`. Credential plaintext is returned once by the issue endpoint and only a salted hash is stored.

Managed clients coexist with legacy configuration-seeded `client_credentials` clients, which continue to use standard OpenIddict application secrets. Register `IServicePrincipalLifecycleListener` to enforce product-specific governance before disable.
