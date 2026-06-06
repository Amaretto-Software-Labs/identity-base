# Identity Base 0.7.17: Explicit OpenIddict Configuration

This release tightens OpenIddict seeding so it behaves exactly as configured. The previous behavior quietly added default permissions and requirements, which made it easy to get started but also easy to misconfigure production clients. In 0.7.17, what you configure is what you get.

## What changed

### 1) OpenIddict seeding is now strict

Only the permissions and requirements listed in `OpenIddict:Applications[].Permissions` and `OpenIddict:Applications[].Requirements` are seeded. Nothing is added implicitly.

That means you must explicitly include:
- endpoint permissions (`endpoints:authorization`, `endpoints:token`, `endpoints:userinfo` if you call `/connect/userinfo`)
- grant types (`grant_types:authorization_code`, `grant_types:refresh_token` if you use refresh tokens)
- response types (`response_types:code` for PKCE)
- scopes (`scopes:openid`, `scopes:profile`, `scopes:email`, plus your API scopes)
- requirements (`requirements:pkce` for public clients)

### 2) Scope prefixes are normalized

Both `scope:` and `scopes:` are accepted in application permissions and normalized to OpenIddict’s `scp:` prefix internally. This makes custom scopes behave the same way as built-in scopes like `openid` and `email`.

## Why this matters

Strict seeding removes hidden behavior and makes configuration portable across environments. It also avoids “phantom permissions” that were never intended to be granted to a client. If a client can request a scope now, it is because you explicitly granted it.

## Migration checklist

1. **Audit your application permissions**: ensure each client lists every endpoint, grant type, response type, scope, and requirement it needs.
2. **Add refresh token grants explicitly** if your client requests `offline_access`.
3. **Verify your custom scopes** are defined under `OpenIddict:Scopes` and granted as `scopes:<name>` (or `scope:<name>`).

Here is a typical PKCE SPA configuration:

```json
{
  "OpenIddict": {
    "Applications": [
      {
        "ClientId": "spa-client",
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
          "scopes:identity.api"
        ],
        "Requirements": ["requirements:pkce"]
      }
    ],
    "Scopes": [
      { "Name": "identity.api", "DisplayName": "Identity API", "Resources": ["identity.api"] }
    ]
  }
}
```

## Summary

0.7.17 is about removing implicit behavior and making OpenIddict seeding fully transparent. It is a small but important change that makes identity clients easier to reason about and safer to operate.
