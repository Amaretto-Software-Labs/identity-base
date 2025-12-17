# @identity-base/client-core

## Overview
`@identity-base/client-core` is the framework-agnostic TypeScript core used by higher-level clients. It implements the Identity Base authorization-code + PKCE flow, access/refresh token management, and typed API calls for user, profile, and admin endpoints.

## Installation

```bash
npm install @identity-base/client-core
```

## Public API

- `IdentityAuthManager` – imperative API for login/PKCE callback handling, token refresh, profile, MFA, and admin endpoints.
- `TokenManager`, `ApiClient` – lower-level primitives.
- Utilities: `generatePkce`, `createTokenStorage`, `createError`, `enableDebugLogging`.

## Notes
- `startAuthorization()` performs a browser redirect; use it only in a browser environment.
- Framework integrations (React/Angular) are expected to handle routing and lifecycle concerns.

## Change Log
- See `CHANGELOG.md`.

