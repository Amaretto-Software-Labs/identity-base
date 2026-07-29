# Sample Client

React sample app for exercising Identity Base authentication flows against an Identity Host.

## Run

```bash
npm install
npm run dev
```

The development server proxies Identity routes to `https://localhost:5000`.
This keeps cookie and passkey flows same-origin on `http://localhost:5174`;
its proxy removes the `Secure` attribute only from the localhost development
response. The Identity host continues to issue secure cookies.

## Environment

Copy `.env.example` to `.env` and adjust values for your local Identity Host.

Key variables:

- `VITE_API_BASE`: optional Identity Host base URL override. Leave it unset for
  the same-origin development proxy, or set an HTTPS URL when the client is
  also served over HTTPS.
- `VITE_CLIENT_ID`: OpenIddict client id (for example `spa-client`)
- `VITE_AUTHORIZE_REDIRECT`: SPA callback URL (must match registered redirect URI)
- `VITE_AUTHORIZE_SCOPE`: scopes requested during sign-in
- `VITE_EXTERNAL_PROVIDERS`: comma-separated `/auth/external/{provider}` route keys exposed by your host

`VITE_EXTERNAL_PROVIDERS` examples:

- No external providers: `VITE_EXTERNAL_PROVIDERS=`
- One provider: `VITE_EXTERNAL_PROVIDERS=github`
- Multiple providers: `VITE_EXTERNAL_PROVIDERS=github,google`

Provider keys must match the host registration via `AddExternalAuthProvider(provider, scheme, ...)`.
