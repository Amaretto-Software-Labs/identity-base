# Identity Base: What Changed Since 0.7.17 (Up to 0.8.1)

If you are upgrading from `0.7.17`, this release train is mostly about external authentication architecture and safer account-linking behavior. The biggest shift is that external providers are now fully host-driven.

## TL;DR

- Built-in external provider helpers were removed from `Identity.Base` (breaking).
- External auth is now provider-agnostic: hosts explicitly register provider route keys and auth schemes.
- Unknown `/auth/external/{provider}` keys are rejected (`400`) instead of probing arbitrary schemes.
- OpenIddict seeding now normalizes legacy prefixes (`endpoints:*`, `grant_types:*`, `scopes:*`, etc.) to canonical OpenIddict permissions.
- Unlinking external providers is safer:
  - Supports both cookie and bearer auth.
  - Prevents removing the last sign-in method.
- New external login policy options let you control email-based auto-linking and verified-email requirements.

## Breaking Changes

The following built-in helpers were removed:

- `AddConfiguredExternalProviders()`
- `AddGoogleAuth(...)`
- `AddMicrosoftAuth(...)`
- `AddAppleAuth(...)`

You now configure providers in the host and map each route key explicitly with `AddExternalAuthProvider(...)`.

## New External Provider Model

### What changed

External login is now split into two explicit concerns:

1. Register the ASP.NET authentication scheme in your host.
2. Register the public provider key (`/auth/external/{provider}`) to that scheme.

This keeps `Identity.Base` provider-agnostic and avoids hard-coded provider assumptions.

### Example

```csharp
builder.Services
    .AddIdentityBase(builder.Configuration, builder.Environment, configureDbContext)
    .AddExternalAuthProvider("google", "Google", auth =>
    {
        return auth.AddGoogle("Google", options =>
        {
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            options.CallbackPath = "/signin-google";
        });
    });
```

## Safer Provider Resolution

`/auth/external/{provider}` now enforces an explicit allowlist via registered provider mappings.  
If the route key is not registered, the endpoint returns `400 Unknown external provider`.

This prevents accidental challenges against non-external or internal auth schemes.

## OpenIddict Seeding Compatibility

`0.8.1` keeps configuration-driven seeding while adding compatibility normalization for legacy permission strings.

Accepted legacy-style values like:

- `endpoints:authorization`
- `grant_types:authorization_code`
- `response_types:code`
- `scopes:identity.api`
- `requirements:pkce`

are normalized internally to OpenIddict canonical forms (`ept:*`, `gt:*`, `rst:*`, `scp:*`, `ft:*`).

## External Unlink Behavior Improvements

Unlink endpoint behavior was hardened:

- Supports both cookie and bearer authentication contexts.
- Resolves bearer users via `sub`/`nameidentifier` fallback.
- Rejects unlinking when it would remove the final sign-in method.

This avoids account lockout from external-only accounts that unlink their only provider.

## New External Login Policy Options

Configure under `Authentication:External`:

```json
{
  "Authentication": {
    "External": {
      "AutoLinkByEmailOnLogin": true,
      "RequireVerifiedEmailForAutoLinkByEmail": false
    }
  }
}
```

- `AutoLinkByEmailOnLogin` (default `true`): when an external login has no existing provider link, allow matching an existing user by email and link automatically.
- `RequireVerifiedEmailForAutoLinkByEmail` (default `false`): when auto-linking by email, require provider claim evidence such as `email_verified=true`.

## Recommended Upgrade Checklist (0.7.17 -> 0.8.1)

1. Remove usage of deleted provider helpers.
2. Register each external scheme in host startup and map route keys using `AddExternalAuthProvider(...)`.
3. Validate only intended provider keys are exposed to clients.
4. Review `Authentication:External` policy defaults for your security model.
5. Keep OpenIddict application permissions explicit; rely on normalization for legacy prefixes only as a migration aid.
6. Re-test these user flows:
   - external signup
   - external login
   - local signup + external link
   - unlink with both cookie and bearer sessions
   - unlink last sign-in method (should be blocked)

## Summary

Since `0.7.17`, Identity Base moved external auth ownership to the host, tightened provider resolution, hardened unlink behavior, and added configurable account-association policy controls. For most teams, migration is straightforward: explicitly register providers in host startup and choose the email auto-link policy that fits your risk profile.
