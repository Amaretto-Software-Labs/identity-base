# HTTP API Reference (Scopes + Endpoint Discovery)

This document answers two common integration gaps:
1. Which **default scopes** exist (`identity.api`, `identity.admin`) and how to enable them.
2. How to discover **all endpoints and request/response schemas** without a ready-made client.

## Scopes: `identity.api` and `identity.admin`

### What they are
- `identity.api` is a conventional API scope used by the samples and `Identity.Base.AspNet` defaults. Your microservices can require it via `RequireScope("identity.api")`.
- `identity.admin` is the default “admin-gating” scope for admin surfaces:
  - `Identity.Base.Admin`: `IdentityAdmin:RequiredScope` defaults to `identity.admin`.
  - `Identity.Base.Organizations`: `Organizations:Authorization:AdminRequiredScope` defaults to `identity.admin`.

Scopes are **not** the same thing as RBAC permissions. Scopes gate access at the OAuth client level, while permissions (e.g. `users.read`, `admin.organizations.manage`) are enforced per-endpoint based on `identity.permissions` claims.

Managed service principals use the same distinction. `Identity:ServicePrincipals:AllowedScopes` determines which OAuth scopes are granted to a newly created managed client, while its assigned global roles determine `identity.permissions` in newly issued tokens.

### How to add/seed them

1. Define scopes in configuration (including `Resources` so access tokens carry the correct `aud` claim):

```json
{
  "OpenIddict": {
    "Scopes": [
      { "Name": "identity.api", "DisplayName": "Identity API", "Resources": ["identity.api"] },
      { "Name": "identity.admin", "DisplayName": "Identity Admin", "Resources": ["identity.api"] }
    ]
  }
}
```

2. Grant scopes to a client by adding `scopes:<name>` to the client permissions:

```json
{
  "OpenIddict": {
    "Applications": [
      {
        "ClientId": "admin-spa",
        "ClientType": "public",
        "RedirectUris": ["https://app.example.com/auth/callback"],
        "Permissions": [
          "endpoints:authorization",
          "endpoints:token",
          "endpoints:userinfo",
          "grant_types:authorization_code",
          "grant_types:refresh_token",
          "response_types:code",
          "scopes:openid",
          "scopes:profile",
          "scopes:email",
          "scopes:offline_access",
          "scopes:identity.api",
          "scopes:identity.admin"
        ],
        "Requirements": ["requirements:pkce"]
      }
    ]
  }
}
```

OpenIddict seeding is strict: only the permissions and requirements you list for a client are applied. For a typical PKCE SPA, explicitly include:
- `endpoints:authorization`, `endpoints:token`, and `endpoints:userinfo` (if you call `/connect/userinfo`).
- `grant_types:authorization_code` and `response_types:code`.
- `scopes:openid`, `scopes:profile`, `scopes:email`, plus your API scopes.
- `scopes:offline_access` and `grant_types:refresh_token` if you expect refresh tokens.
- `requirements:pkce` for public clients.

If you want admin endpoints to *not* require an OAuth scope, set `IdentityAdmin:RequiredScope` to `null` (permissions still apply).

> Note: clients only receive the scopes you explicitly grant via `OpenIddict:Applications[].Permissions` (e.g. `scopes:identity.api`, `scopes:identity.admin`). The built-in OpenIddict seeder no longer blanket-grants every configured scope to every client.

## Endpoint specs (OpenAPI)

Identity Base uses ASP.NET Core OpenAPI. When you call `app.UseApiPipeline()`, it maps OpenAPI endpoints **in Development only**.

- OpenAPI JSON: `GET /openapi/v1.json`
- Generated endpoint list: `docs/reference/openapi-endpoints.md`
- Raw captured OpenAPI JSON: `docs/reference/openapi-org-sample-v1.json`

### Why this matters

If you’re integrating without a client SDK, OpenAPI is the authoritative source for:
- available routes (`/auth/*`, `/users/*`, `/admin/*`, `/admin/organizations/*`, etc.)
- HTTP methods
- request and response schemas
- status codes

### Quick ways to inspect

List paths:

```bash
curl -s https://localhost:5000/openapi/v1.json | jq -r '.paths | keys[]'
```

List operations (method + path):

```bash
curl -s https://localhost:5000/openapi/v1.json | jq -r '.paths | to_entries[] | .key as $p | .value | keys[] as $m | \"\\($m|ascii_upcase) \\($p)\"'
```

> Note: the OpenAPI document reflects whichever packages you mapped in your host (e.g. `app.MapIdentityAdminEndpoints()`, `app.MapIdentityBaseOrganizationEndpoints()`). If you don’t map a module, it won’t show up in OpenAPI.

## Managed Service Principal Endpoints

When the host registers `Identity.Base.ServicePrincipals` and calls `MapIdentityBaseServicePrincipalEndpoints()`, OpenAPI also includes twelve routes beneath `/admin/service-principals` for principal lifecycle, roles, and credentials. They use the normal admin scope plus `service-principals.*` permissions. See the [package reference](../packages/identity-base-service-principals/index.md#admin-api) for the route matrix.

The token exchange itself uses the standard OpenIddict endpoint:

```bash
curl -sS https://identity.example.com/connect/token \
  -u "$CLIENT_ID:$CLIENT_SECRET" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'scope=identity.api'
```

Managed machine tokens contain a `Guid` `sub`, `client_id`, `identity.principal_type=ServicePrincipal`, and role-derived `identity.permissions`. They do not receive refresh tokens.
