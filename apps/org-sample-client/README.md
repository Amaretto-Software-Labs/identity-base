# Organization Sample Client

Companion SPA for `apps/org-sample-api`. This React + Vite application demonstrates how to exercise the multi-organization scenario end to end using the published Identity Base packages and the sample-only invitation overlay.

## Features
- **Registration** – renders the profile schema (including organization slug/name fields) and submits to `/auth/register`.
- **Authentication** – uses `@identity-base/react-client` for sign-in, session management, and API calls.
- **Dashboard** – lists memberships from `/users/me/organizations`, allows switching the active organization (via `X-Organization-Id`), and links to management pages.
- **Organization management** – displays members/roles via `/users/me/organizations/{id}/members` & `/users/me/organizations/{id}/roles`, and exposes invitation CRUD using the sample endpoints.
- **Invitation redemption** – redeem invitation codes from `/sample/invitations/claim` after signing in.

## Getting Started
```bash
cd apps/org-sample-client
npm install
npm run dev
```

The dev server listens on `http://localhost:5173` by default (override with `PORT`). Configure the base URL and OpenIddict client via Vite env variables (`.env.local`):

```bash
VITE_API_BASE=https://localhost:8182
VITE_CLIENT_ID=org-sample-client
VITE_AUTHORIZE_REDIRECT=http://localhost:5173/auth/callback
VITE_AUTHORIZE_SCOPE=openid profile email offline_access identity.api
```

> Ensure `apps/org-sample-api` is running, pointing at PostgreSQL databases, and configured with an OpenIddict application that matches the client ID and redirect URI.

## Notable Routes
- `/` – scenario overview and quick links.
- `/register` – collects email/password and organization metadata.
- `/login` – basic credential sign-in flow.
- `/dashboard` – membership list, active organization setter, and links to management.
- `/organizations/:id` – invitation management plus member/role visibility.
- `/invitations/claim` – accept invitation codes as the currently signed-in user.

## Building
```bash
npm run build
```

The build invokes TypeScript and `vite build`, outputting production assets under `dist/`.
