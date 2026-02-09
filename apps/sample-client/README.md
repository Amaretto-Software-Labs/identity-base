# Sample Client

React sample app for exercising Identity Base authentication flows against an Identity Host.

## Run

```bash
npm install
npm run dev
```

## Environment

Copy `.env.example` to `.env` and adjust values for your local Identity Host.

Key variables:

- `VITE_API_BASE`: Identity Host base URL (for example `https://localhost:5000`)
- `VITE_CLIENT_ID`: OpenIddict client id (for example `spa-client`)
- `VITE_AUTHORIZE_REDIRECT`: SPA callback URL (must match registered redirect URI)
- `VITE_AUTHORIZE_SCOPE`: scopes requested during sign-in
- `VITE_EXTERNAL_PROVIDERS`: comma-separated `/auth/external/{provider}` route keys exposed by your host

`VITE_EXTERNAL_PROVIDERS` examples:

- No external providers: `VITE_EXTERNAL_PROVIDERS=`
- One provider: `VITE_EXTERNAL_PROVIDERS=github`
- Multiple providers: `VITE_EXTERNAL_PROVIDERS=github,google`

Provider keys must match the host registration via `AddExternalAuthProvider(provider, scheme, ...)`.
