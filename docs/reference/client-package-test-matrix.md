# Client Package Test Matrix

This document defines the minimum test coverage expectations for the npm client packages under `packages/`.

The goal is to keep CI coverage thresholds meaningful without blocking delivery on low-risk wrappers. Thresholds are intended to ratchet upward over time.

## Principles

- Prefer testing **public API behavior** over internal implementation details.
- Cover **error mapping**, **auth header attachment**, and **URL/query building** (highest risk / most regressions).
- Keep tests **fast** (Node test runner + small mocks); avoid spinning up real servers.

## Coverage Gates (Current Targets)

These are CI thresholds for `npm run coverage` (coverage is measured from built `dist/` output).

| Package | Lines | Functions | Branches |
| --- | ---: | ---: | ---: |
| `@identity-base/client-core` | 80% | 80% | 80% |
| `@identity-base/angular-client` | 80% | 80% | 80% |
| `@identity-base/angular-organizations` | 80% | 80% | 80% |
| `@identity-base/react-client` | 25% | 20% | 40% |
| `@identity-base/react-organizations` | 30% | 20% | 40% |

## Required Test Coverage by Package

### `@identity-base/client-core`
- `ApiClient.fetch`:
  - success: JSON, empty body, `204`, `parse: 'text'`
  - failures: JSON problem details, plain text body, invalid JSON, timeout abort
- `TokenManager`:
  - `exchangeAuthorizationCode` form body + token storage side effects
  - refresh coalescing + refresh failure clears tokens
  - `autoRefresh: false` expired tokens cleared
- `PKCEManager`:
  - persist/consume behavior (single-consume semantics)
- `IdentityAuthManager`:
  - authorization-code PKCE start + callback, event emission, authorized calls
  - at least one admin endpoint URL/query builder path
- `createError` formatting:
  - validation errors map → flattened message

### `@identity-base/angular-client`
- `provideIdentityClient` defaults (`tokenStorage`, `autoRefresh`)
- `IdentityAuthInterceptor` include/exclude and “existing Authorization header” behavior
- `IdentityAuthService`:
  - `snapshot` and `refreshUser` error propagation
  - browser-only guards (`startAuthorization`, `handleAuthorizationCallback`)

### `@identity-base/angular-organizations`
- `provideIdentityOrganizations` defaults
- `OrganizationContextInterceptor` include/exclude and header name override
- `OrganizationsService`:
  - auth/no-auth endpoints
  - header attachment rules + query string building + id encoding
  - error mapping (problem details and timeouts)

### `@identity-base/react-client`
- Provider:
  - initialization + config change behavior
- Hooks/components:
  - at least one test each for: `useLogin`, `useProfile`, `usePermissions`, `ProtectedRoute` or `RequireAuth`

### `@identity-base/react-organizations`
- OrganizationsProvider:
  - bearer token attachment
  - org header behavior and persistence (storage key, default exclusion for user org routes)
