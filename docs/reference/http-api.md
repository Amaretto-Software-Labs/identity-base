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
