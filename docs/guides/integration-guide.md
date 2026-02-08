# Integration Guide

This guide describes how to exercise Identity Base end-to-end using the sample React harness located at `apps/sample-client`. The harness demonstrates registration, login with MFA, profile management, external provider linking, and the authorization-code-with-PKCE flow. Use it as a reference implementation when integrating your own SPA or native client.

## 1. Prerequisites
- Identity Base API running locally (for example via `docker-compose.local.yml`).
- Node.js 20+ and npm 10+.
- MailJet or SendGrid credentials (or placeholder values for local testing).
- Optional: configured external provider credentials for any OAuth/OIDC scheme you register in the host.

## 2. Start the API Stack
Follow the [Docker guide](./docker.md) to bring up PostgreSQL, MailHog, and the API:

```bash
docker compose -f docker-compose.local.yml --env-file .env up --build
```

Verify the API is reachable at `http://localhost:8080` and that `/healthz` reports `Healthy` for required checks such as `database` and `mailjet`.

## 3. Configure the Harness
From the repository root:

```bash
cd apps/sample-client
cp .env.example .env
```

Update `.env` with values that match your API deployment:

| Variable | Description |
| --- | --- |
| `VITE_API_BASE` | Base URL for the Identity Base host (defaults to `https://localhost:5000`). |
| `VITE_SAMPLE_API_BASE` | Base URL for the companion `apps/sample-api` (defaults to `https://localhost:8199`). |
| `VITE_CLIENT_ID` | Client ID used for login and PKCE (defaults to `spa-client`). |
| `VITE_AUTHORIZE_REDIRECT` | Redirect URI registered with Identity Base (`http://localhost:5174/auth/callback` by default). |
| `VITE_AUTHORIZE_SCOPE` | Space-delimited scopes requested during authorization (default includes `identity.api`). |
| `VITE_EXTERNAL_PROVIDERS` | Comma-separated `/auth/external/{provider}` route keys exposed by your host (for example `github,google`). |

## 4. Install Dependencies & Run Dev Server

```bash
npm install
npm run dev
```

Vite serves the app at `http://localhost:5174` by default (override with `PORT`). The SPA calls the Identity Base host directly using `VITE_API_BASE`, so ensure:
- the host has CORS enabled for your SPA origin, and
- your OpenIddict client uses the same redirect URI as `VITE_AUTHORIZE_REDIRECT`.

If you run the API stack via Docker Compose, set `VITE_API_BASE=http://localhost:8080` (and keep `VITE_AUTHORIZE_REDIRECT=http://localhost:5174/auth/callback`).

## 5. Supported Journeys
- **Registration:** loads the dynamic schema from `/auth/profile-schema`, validates inputs client-side, and posts to `/auth/register`.
- **Login & MFA:** submits credentials to `/auth/login`, displays available second-factor methods, and drives `/auth/mfa/challenge` + `/auth/mfa/verify` when necessary.
- **Profile Management:** pulls `/users/me`, renders metadata fields, saves via `PUT /users/me/profile`, and exposes link/unlink buttons for external providers.
- **External Providers:** launches `/auth/external/{provider}/start` in login or link mode and surfaces the redirect outcome at `/external-result`.
- **Authorization Code with PKCE:** generates a PKCE challenge, navigates to `/connect/authorize`, and exchanges the returned code on the callback screen.

Each workflow reads and displays server responses (success messages, validation problems, audit-visible actions) to aid manual testing.

## 6. Token Exchange Testing
After completing consent, the callback page stores the authorization code and lets you exchange it for tokens by calling `/connect/token`. The raw token response is rendered for inspection—useful when validating scopes and expiry.

## 7. Tips & Troubleshooting
- Ensure your SPA origin is listed in `Cors:AllowedOrigins`. Browser calls to `/auth/*` are rejected with `403` if the `Origin` is not allowed. Session cookies are SameSite=Lax and only keep the session on the Identity host; SPAs should use access tokens for API calls.
- When testing external providers, register each scheme in the host with `AddExternalAuthProvider(provider, scheme, ...)` and set matching keys in `VITE_EXTERNAL_PROVIDERS`.
- Use MailHog (`http://localhost:8025`) to verify confirmation and MFA emails if real MailJet/SendGrid credentials are not configured.
- Clear PKCE values via the “Clear stored PKCE verifier” button if you restart flows mid-way.

## 8. ASP.NET Core API Integration

For .NET developers who need to protect their APIs with Identity.Base JWT tokens, use the `Identity.Base.AspNet` integration library. This simplifies JWT Bearer authentication setup with pre-configured extension methods.

### Quick Setup

Add the project reference or install the library:
```xml
<ProjectReference Include="../../Identity.Base.AspNet/Identity.Base.AspNet.csproj" />
```

Configure in your `Program.cs`:
```csharp
using Identity.Base.AspNet;

var builder = WebApplication.CreateBuilder(args);

// Add Identity.Base JWT authentication
builder.Services.AddIdentityBaseAuthentication("https://localhost:5000");

var app = builder.Build();

// Add middleware
app.UseIdentityBaseRequestLogging(enableDetailedLogging: true);
app.UseIdentityBaseAuthentication();

// Protect endpoints
app.MapGet("/api/protected/data", () => "Protected data")
    .RequireAuthorization();

// Require specific scopes
app.MapGet("/api/admin", () => "Admin data")
    .RequireAuthorization(policy => policy.RequireScope("identity.api"));
```

### Sample API
See `apps/sample-api/` for a complete working example that demonstrates:
- Public and protected endpoints
- Scope-based authorization
- User profile access
- JWT claims inspection

For detailed configuration options, troubleshooting, and API reference, see the [Identity.Base.AspNet README](../Identity.Base.AspNet/README.md).

## 9. Building for Production
Generate a production bundle with:

```bash
npm run build
```

The assets in `dist/` can be deployed behind a static host (e.g., Nginx, Azure Static Web Apps). Ensure the runtime environment supplies the same client ID and redirect URI values configured in Identity Base.
