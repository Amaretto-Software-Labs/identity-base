# Getting Started

This guide walks through configuring and running Identity Base in a local environment.

## Prerequisites
- .NET SDK 9.0+
- PostgreSQL 16 (local or containerised)
- Optional: Docker Desktop for running the provided Postgres compose file

## Setup Steps
1. Clone the repository and restore dependencies:
   ```bash
   dotnet restore Identity.sln
   ```
2. Configure the database connection string in `Identity.Base/appsettings.Development.json` or via environment variables.
3. Adjust registration metadata in the `Registration` section. Each `ProfileField` entry defines:
   - `Name`: Key used in registration payload metadata
   - `DisplayName`: Human readable label
   - `Required`: Whether the field must be supplied
   - `MaxLength`: Maximum character length
   - `Pattern`: Optional regular expression for server-side validation
4. Replace the MailJet placeholders (`MailJet:ApiKey`, `MailJet:ApiSecret`, `MailJet:FromEmail`, `MailJet:Templates:Confirmation`, `MailJet:Templates:PasswordReset`, `MailJet:Templates:MfaChallenge`) with valid values and, if you want operational alerts, enable `MailJet:ErrorReporting` with a monitored inbox. The service will fail to start without these credentials.
5. Configure OpenIddict applications/scopes under the `OpenIddict` section (client IDs, redirect URIs, permissions, resources). The default sample client targets a local SPA.
6. Provide the MFA issuer name via `Mfa:Issuer` (this is the label shown in authenticator apps when users enrol). Use the nested `Mfa:Email:Enabled` and `Mfa:Sms` settings to decide which challenge methods are available; when SMS is enabled, populate the Twilio credentials inside `Mfa:Sms` (`AccountSid`, `AuthToken`, `FromPhoneNumber`).
7. (Optional) Enable social sign-in by configuring the `ExternalProviders` section. Each provider exposes `Enabled`, `ClientId`, `ClientSecret`, `CallbackPath`, and `Scopes`; Apple additionally supports `TeamId`, `KeyId`, and an inline `PrivateKey` for JWT-based client secrets.
8. (Optional) Enable the seed administrator account by setting `IdentitySeed:Enabled` to `true` and providing `Email`, `Password`, and `Roles`.
9. Apply database migrations:
   ```bash
   dotnet ef database update --project Identity.Base/Identity.Base.csproj
   ```
10. Run the service:
   ```bash
   dotnet run --project Identity.Base/Identity.Base.csproj
   ```
11. Submit a registration request with metadata:
   ```bash
   curl -X POST https://localhost:5001/auth/register \
     -H "Content-Type: application/json" \
     -d '{
       "email": "user@example.com",
       "password": "Passw0rd!Passw0rd!",
       "metadata": {
         "displayName": "Example User",
         "company": "Example Co"
       }
     }'
   ```

## Email Templates
- MailJet integration is always on. Populate `MailJet` API credentials, sender details, template ids (confirmation, password reset, MFA challenge), and (optionally) enable `MailJet:ErrorReporting` to receive delivery failures.
- When enabled, `/auth/register` sends the confirmation template with the following variables:
  - `email`
  - `displayName`
  - `confirmationUrl`
- `/auth/forgot-password` leverages the password reset template with variables `email`, `displayName`, and `resetUrl`.
- `/auth/mfa/challenge` (email method) uses the MFA challenge template with variables `email`, `displayName`, and `code`.

## Running Tests
- Integration tests run against the EF Core in-memory provider. Execute `dotnet test Identity.sln` before opening a pull request.

## SPA Authentication Flow

Single-page applications interact with the identity service in two phases:

1. **Credential Sign-In** – submit the user’s email/password to `/auth/login`:
   ```http
   POST /auth/login
   Content-Type: application/json

   {
     "email": "user@example.com",
     "password": "Passw0rd!Passw0rd!",
     "clientId": "spa-client"
   }
   ```
   A successful response returns `200 OK` and sets the Identity cookie that backs subsequent OpenID Connect requests.

2. **Authorization Code with PKCE** – once the cookie is present, initiate the OpenIddict flow from the SPA by navigating to `/connect/authorize` with the usual PKCE parameters (`response_type=code`, `client_id`, `redirect_uri`, `scope`, `code_challenge`, `code_challenge_method`, optional `state`).

   If the session is missing, the server replies with `401 Unauthorized` and a `WWW-Authenticate: error="login_required"` header. The SPA should interpret this as “show the login screen”, obtain a new cookie via `/auth/login`, and retry `/connect/authorize`.

3. **Token Exchange** – the SPA exchanges the returned authorization `code` for tokens by POSTing to `/connect/token` with the corresponding `code_verifier` and redirect URI.

This mirrors the hosted-provider experience (e.g., Auth0 Universal Login) while keeping all credential UX inside the SPA.

4. **Logout** – to clear the Identity session, POST to `/auth/logout`. A subsequent `/connect/authorize` call will again yield `401 Unauthorized` until the SPA signs the user back in.

### MFA Flow (Optional)

If multi-factor authentication is enabled for an account:

1. **Enroll** – authenticated users call `/auth/mfa/enroll` to retrieve the shared key and `otpauth` URI (render as QR in the SPA). They verify the initial code via `/auth/mfa/verify` which enables MFA and returns recovery codes.
2. **Step-Up During Login** – when `/auth/login` responds with `{ "requiresTwoFactor": true, "methods": [ ... ] }`, prompt for the desired method (authenticator, SMS, recovery). Use `/auth/mfa/challenge` to send an SMS code when supported, then POST the code to `/auth/mfa/verify`. A successful response completes the sign-in.
3. **Recovery & Disable** – authenticated users can regenerate recovery codes (`/auth/mfa/recovery-codes`) or disable MFA (`/auth/mfa/disable`).

## External Sign-In

- Initiate Google, Microsoft, or Apple sign-in by calling `GET /auth/external/{provider}/start?returnUrl=/app/auth/callback`. The `returnUrl` must be a relative path, and the backend redirects there after processing the provider callback with `status`, `requiresTwoFactor`, and optional `methods` query parameters.
- Add `mode=link` to the start request while authenticated to attach the external identity to the current user (e.g., `/auth/external/google/start?mode=link&returnUrl=/account/connections`).
- Provider callbacks are handled at `/auth/external/{provider}/callback`; the server issues the Identity cookie before redirecting.
- Remove a linked provider with `DELETE /auth/external/{provider}` (requires authentication).

## Profile Metadata API

- Retrieve the configured profile fields with `GET /auth/profile-schema` to build registration/profile forms dynamically.
- Fetch the signed-in user's profile via `GET /users/me` (requires the Identity cookie) to obtain metadata values and the `concurrencyStamp`.
- Persist changes with `PUT /users/me/profile`, passing the full metadata map and the current `concurrencyStamp`. Validation follows the schema (required, max length, optional regex), and the response includes the updated stamp for subsequent edits.

## Observability & Health

- Every request pushes `CorrelationId` (ASP.NET trace identifier) and `UserId` (if authenticated) into Serilog's scope. Audit actions are emitted via the `IAuditLogger` for MFA operations, profile updates, and external-provider link/unlink events.
- `/healthz` now reports database, MailJet configuration, and external-provider readiness in the `checks` payload. Use it for container liveness/readiness probes.
