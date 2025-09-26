# Integration Guide

This guide describes how to exercise Identity Base end-to-end using the sample React harness located at `apps/sample-client`. The harness demonstrates registration, login with MFA, profile management, external provider linking, and the authorization-code-with-PKCE flow. Use it as a reference implementation when integrating your own SPA or native client.

## 1. Prerequisites
- Identity Base API running locally (for example via `docker-compose.local.yml`).
- Node.js 20+ and npm 10+.
- MailJet credentials (or placeholder values for local testing).
- Optional: configured social provider credentials (Google, Microsoft, Apple).

## 2. Start the API Stack
Follow the [Docker guide](./docker.md) to bring up PostgreSQL, MailHog, and the API:

```bash
docker compose -f docker-compose.local.yml --env-file .env up --build
```

Verify the API is reachable at `http://localhost:8080` and that `/healthz` reports `Healthy` for the `database`, `mailjet`, and `externalProviders` checks.

## 3. Configure the Harness
From the repository root:

```bash
cd apps/sample-client
cp .env.example .env
```

Update `.env` with values that match your API deployment:

| Variable | Description |
| --- | --- |
| `VITE_API_BASE` | Optional base URL for all API requests (defaults to relative `/`). |
| `VITE_CLIENT_ID` | Client ID used for login and PKCE (defaults to `spa-client`). |
| `VITE_AUTHORIZE_REDIRECT` | Redirect URI registered with Identity Base (`http://localhost:5173/auth/callback` by default). |
| `VITE_AUTHORIZE_SCOPE` | Space-delimited scopes requested during authorization (default includes `identity.api`). |
| `VITE_EXTERNAL_*` | Set to `true` to enable the corresponding external provider buttons. |

## 4. Install Dependencies & Run Dev Server

```bash
npm install
npm run dev
```

Vite serves the app at `http://localhost:5173`. The dev server proxies `/auth`, `/users`, `/connect`, and `/healthz` to the API running on port 8080 (see `vite.config.ts`). Adjust the proxy targets if your API listens elsewhere.

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
- Ensure the browser accepts cookies from `localhost:8080`; the Identity cookie powers MFA and profile endpoints.
- When testing external providers, configure the same redirect URL in the provider console and set the `VITE_EXTERNAL_*` flag to `true`.
- Use MailHog (`http://localhost:8025`) to verify confirmation and MFA emails if real MailJet credentials are not configured.
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
