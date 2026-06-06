# Identity Base: Changes Since 0.7.9 (and How to Upgrade to 0.7.16)

If you are upgrading from 0.7.9, the main impacts are around browser auth security hardening and client ergonomics. There are no new API surface breaking changes called out after 0.7.9, but a couple of behavior changes can affect real deployments. This post summarizes what changed, how to adopt it, and a practical migration path.

## TL;DR

- 0.7.12 tightens browser auth flows: `/auth/*` now validates `Origin` against `Cors:AllowedOrigins`, and cookies are `SameSite=Lax`.
- Angular client packages now ship Ivy partial compilation output for AOT and include `IdentityRequireAuthGuard`.
- 0.7.15 adds permission claim helpers for server-side checks.
- 0.7.16 removes the custom password rules and fully respects ASP.NET Core Identity password validation settings.
- One post-0.7.9 commit (not called out in the changelog) allows Bearer auth on `/users/me`.

## Changes by release

### 0.7.12

**Behavior changes (potentially breaking):**
- `/auth/*` endpoints now enforce browser Origin checks against `Cors:AllowedOrigins`. If your SPA calls `/auth/login`, `/auth/register`, etc. from a browser and the origin is not in `Cors:AllowedOrigins`, the request will be rejected.
- Authentication cookies now use `SameSite=Lax`. This keeps cookies scoped to the Identity host by default. Cross-origin SPAs should use access tokens rather than cookie auth.

**Client packages:**
- Angular client packages now ship ng-packagr/Ivy partial compilation output for AOT compliance.
- Added `IdentityRequireAuthGuard` and improved auth loading and error handling.

**Docs/Samples/CI:**
- Sample apps and docs updated for new local ports and origin configuration.
- CI now produces and uploads coverage reports.

### 0.7.15

**Server-side utilities:**
- Added permission claim utilities in `Identity.Base.Roles` and organization claim helpers in `Identity.Base.Organizations` for consistent authorization checks.

### 0.7.16

**Behavior:**
- Password validation now fully respects ASP.NET Core Identity password validation settings. The custom Identity Base password configuration is removed, so ensure your `IdentityOptions.Password` values reflect your policy.

### Additional post-0.7.9 change not called out in the changelog

- `/users/me` endpoints now accept Bearer auth in addition to cookies. This makes SPA token-based access easier when you are not on the same origin as the Identity host.

## What clients need to do

### Browser-based apps (React, Angular, etc.)

1. Add your SPA origin to `Cors:AllowedOrigins` so `/auth/*` endpoints accept requests.
2. Use access tokens for cross-origin API calls. Cookie-based auth from another origin will no longer work as before because of `SameSite=Lax`.
3. If you are using `/users/me` endpoints, you can now call them with `Authorization: Bearer <access_token>` instead of relying on cookies.

### Angular clients

- Update to the latest `@identity-base/angular-client` and `@identity-base/angular-organizations` packages.
- If you want route protection out of the box, use the new `IdentityRequireAuthGuard`.

### Server/host applications

1. Ensure `Cors:AllowedOrigins` includes every browser origin that will hit `/auth/*` endpoints.
2. Decide whether you are using cookie auth (same-origin) or access tokens (cross-origin) and document this for clients.
3. Review `IdentityOptions.Password.RequiredLength` (and other password settings) to match your desired policy.
4. Optional: adopt the new permission/organization claim helpers from 0.7.15 to simplify server-side checks.

## Migration path (recommended)

If you are on 0.7.9 today, upgrading directly to 0.7.16 is fine, but use this checklist to avoid surprises:

1. **Upgrade packages** to `0.7.16` (server and client libraries).
2. **Update CORS**: set `Cors:AllowedOrigins` to all browser origins that call `/auth/*`.
3. **Confirm auth strategy**:
   - Same-origin apps can keep cookie auth.
   - Cross-origin SPAs should use PKCE + access tokens, and call `/users/me` with Bearer tokens.
4. **Validate password policy**: ensure your `IdentityOptions.Password` settings match your desired policy (length, complexity, etc.).
5. **Adopt optional helpers**: if you do permission checks server-side, consider the new claim helpers in 0.7.15.
6. **Run smoke tests**: login, registration, password reset, and `/users/me` calls from the browser.

## Example: minimal CORS config

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://admin.example.com"
    ]
  }
}
```

## Summary

The big shift since 0.7.9 is tighter browser security around `/auth/*` and cookie handling, which pushes cross-origin SPAs toward token-based auth. The rest of the changes are incremental: better Angular packaging, new claim helpers, and a few quality-of-life improvements for token usage and password policy enforcement.
