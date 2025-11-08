# React Integration Guide

This document walks a junior front-end developer through everything required to integrate Identity Base into a brand-new React application alongside an ASP.NET Core API. It covers configuration, required UI flows, package usage, and backend expectations.

---

## 1. Prerequisites

| Tool | Recommended Version | Notes |
| --- | --- | --- |
| Node.js | 20.x | Install with nvm or the official installer |
| npm | 10.x | Ships with Node 20 |
| .NET SDK | 9.0.x | Builds the Identity Base host & your API |
| PostgreSQL | 16.x | Identity Base stores users & clients here |
| MailJet account | Optional (only when using the Mailjet email package) | Configure API Key/Secret & template IDs |

You also need an Identity Base deployment (local host app or shared environment). Follow [docs/guides/getting-started.md](./getting-started.md) to run the host locally if needed.

---

## 2. Identity Base Host Configuration

Before wiring the UI, confirm the Identity Base host exposes the following endpoints:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/forgot-password`
- `POST /auth/reset-password`
- `POST /auth/mfa/challenge`
- `POST /auth/mfa/verify`
- `POST /auth/mfa/enroll`
- `POST /auth/mfa/disable`
- `POST /auth/mfa/recovery-codes`
- `GET /auth/profile-schema`
- `GET /users/me`
- `PUT /users/me/profile`
- External auth start: `GET /auth/external/{provider}/start`

Configure the host (`Identity.Base.Host/appsettings.Development.json`) with:

- `Registration:ConfirmationUrlTemplate`
- `Registration:PasswordResetUrlTemplate`
- MailJet API credentials + template IDs (`Confirmation`, `PasswordReset`, `MfaChallenge`) when the Mailjet add-on is enabled
- CORS allowed origins (`http://localhost:5173` for local React app)
- External provider credentials if applicable

Apply migrations (from your host project) so the schema matches the running code. Example:

```bash
dotnet ef database update \
  --project Identity.Base.Host/Identity.Base.Host.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj \
  --context Identity.Base.Data.AppDbContext
```

Repeat for any additional contexts you enabled (e.g., `IdentityRolesDbContext`, `OrganizationDbContext`).

---

## 3. Create the React project

1. Scaffold a Vite + React + TypeScript app:

   ```bash
   npm create vite@latest my-identity-app -- --template react-ts
   cd my-identity-app
   npm install
   ```

2. Install the Identity Base React client:

   ```bash
   npm install @identity-base/react-client
   ```

3. Create an `.env` file with the SPA configuration:

   ```env
   VITE_API_BASE=https://localhost:5000
   VITE_CLIENT_ID=spa-client
   VITE_AUTHORIZE_REDIRECT=http://localhost:5173/auth/callback
   VITE_AUTHORIZE_SCOPE="openid profile email offline_access identity.api"
   VITE_EXTERNAL_GOOGLE_ENABLED=false
   VITE_EXTERNAL_MICROSOFT_ENABLED=false
   VITE_EXTERNAL_APPLE_ENABLED=false
   ```

Adjust the base URL and client ID to match your Identity Base host configuration.

---

## 4. Wrap the app with `IdentityProvider`

In `src/main.tsx` or the root component, wrap your routing tree:

```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { CONFIG } from './config'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <IdentityProvider
      config={{
        apiBase: CONFIG.apiBase,
        clientId: CONFIG.clientId,
        redirectUri: CONFIG.authorizeRedirectUri,
        scope: CONFIG.authorizeScope,
        tokenStorage: 'localStorage',
      }}
    >
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </IdentityProvider>
  </React.StrictMode>,
)
```

Keep `CONFIG` in a separate file (`src/config.ts`) so you can pull values from `import.meta.env`.

---

## 5. Required UI workflows

Implement the following pages/components.

### 5.1 Registration
- Use `useRegister` from the React client.
- Fetch the profile schema via `authManager.getProfileSchema()` to render dynamic fields.
- POST to `/auth/register`; show the correlation ID returned and prompt users to confirm email.

### 5.2 Email confirmation (optional UI)
- When users click the email link, send them to a simple “confirmation success” page that calls `POST /auth/confirm-email` with the `token` & `userId` from the query string.

### 5.3 Login
- Use `useLogin` hook.
- Handle `response.requiresTwoFactor` by redirecting to an MFA challenge page.
- Provide links to external providers (call `authManager.buildExternalStartUrl(provider, 'login', window.location.origin)`).

### 5.4 Forgot password & reset password
- Forgot password page: call `useForgotPassword().requestReset(email)`.
- Reset password page: read `token` and `userId` from query string, validate new password inputs, and call `useResetPassword().resetPassword({ userId, token, password })`.

### 5.5 MFA flows
- Challenge page: call `useMfa().sendChallenge` (for SMS/email) and `useMfa().verify` with the chosen method.
- Setup page: call `authManager.enrollMfa()` to display `sharedKey` and QR code, then `useMfa().verify` to complete enrollment.
- Allow users to disable MFA via `authManager.disableMfa()` and regenerate recovery codes with `authManager.regenerateRecoveryCodes()`.

### 5.6 Profile management
- Create a profile page using `useProfile()` to fetch `GET /users/me`.
- Render dynamic metadata fields based on the schema (re-use registration metadata array).
- Submit changes via `authManager.updateProfile({ metadata, concurrencyStamp })`.

### 5.7 External provider linking
- Provide buttons to link (call `buildExternalStartUrl(provider, 'link', returnUrl, { email, name })`) and unlink (`authManager.unlinkExternalProvider(provider)`).

### 5.8 Authorization Code flow (optional)
- Use `authManager.startAuthorization()` to send users to `/connect/authorize`.
- On the callback screen, call `authManager.handleAuthorizationCallback(code, state)` to exchange the code for tokens.
- Display token information for debugging.

---

## 6. API (resource server) integration

In any ASP.NET Core API that needs to consume Identity Base tokens, install the NuGet package:

```bash
dotnet add package Identity.Base.AspNet
```

Configure in `Program.cs`:

```csharp
using Identity.Base.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBaseAuthentication("https://localhost:5000", "identity.api");

var app = builder.Build();
app.UseIdentityBaseRequestLogging();
app.UseIdentityBaseAuthentication();

app.MapGet("/api/protected", () => "Secure data").RequireAuthorization();

app.Run();
```

This sets up JWT Bearer authentication, logs inbound requests, and checks scopes via simple policies (e.g., `policy.RequireScope("identity.api")`).

---

## 7. Environment-specific settings

| Scenario | What to update |
| --- | --- |
| Development | Use localhost URLs, allow self-signed certificates, enable MailHog or MailJet sandbox |
| Staging/Production | Switch `apiBase` and redirect URIs to the deployed host, update CORS origins, provide real MailJet credentials, configure external provider redirect URIs |
| SPA deployment | Serve built assets from static host; ensure environment variables (client ID, redirect URI) are injected at build time |

Remember to update Identity Base `appsettings` for each environment (confirm redirect URIs and allowed origins).

---

## 8. Testing checklist

- Register a new user and verify the confirmation email.
- Log in with password, complete MFA, and access protected endpoints.
- Trigger forgot password → reset password; ensure new password works.
- Link/unlink external providers.
- Update profile metadata (including concurrency stamp handling).
- Run PKCE flow and retrieve tokens for API calls.
- For APIs, hit protected endpoints with the SPA-issued JWT to confirm authorization.
> See also: Task Playbook — docs/playbooks/react-client-pkce-login.md for copy-ready PKCE and SPA run commands.

---

## 9. Helpful references

- [Getting Started with Identity Base](./getting-started.md)
- [Identity.Base Public API surface](../reference/identity-base-public-api.md)
- [Release Checklist](../release/release-checklist.md)
- Sample implementations: `apps/sample-client` (React), `apps/sample-api` (ASP.NET Core)

With these steps, a junior developer can scaffold a full-featured React UI powered by Identity Base, wire it to an existing or greenfield .NET API, and confidently support core authentication journeys.
