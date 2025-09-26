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
2. Configure the database connection string in `Identity.Base.Host/appsettings.Development.json` or via environment variables.
3. Adjust registration metadata in the `Registration` section. Each `ProfileField` entry defines:
   - `Name`: Key used in registration payload metadata
   - `DisplayName`: Human readable label
   - `Required`: Whether the field must be supplied
   - `MaxLength`: Maximum character length
   - `Pattern`: Optional regular expression for server-side validation
4. Replace the MailJet placeholders (`MailJet:ApiKey`, `MailJet:ApiSecret`, `MailJet:FromEmail`, `MailJet:Templates:Confirmation`, `MailJet:Templates:PasswordReset`, `MailJet:Templates:MfaChallenge`) with valid values and, if you want operational alerts, enable `MailJet:ErrorReporting` with a monitored inbox. The service will fail to start without these credentials.
5. Configure OpenIddict applications/scopes under the `OpenIddict` section (client IDs, redirect URIs, permissions, resources). The default sample client targets a local SPA. Persist signing/encryption keys by setting `OpenIddict:ServerKeys` (see “Server Key Providers” below).
6. Provide the MFA issuer name via `Mfa:Issuer` (this is the label shown in authenticator apps when users enrol). Use the nested `Mfa:Email:Enabled` and `Mfa:Sms` settings to decide which challenge methods are available; when SMS is enabled, populate the Twilio credentials inside `Mfa:Sms` (`AccountSid`, `AuthToken`, `FromPhoneNumber`).
7. (Optional) Enable social sign-in by configuring the `ExternalProviders` section. Each provider exposes `Enabled`, `ClientId`, `ClientSecret`, `CallbackPath`, and `Scopes`; Apple additionally supports `TeamId`, `KeyId`, and an inline `PrivateKey` for JWT-based client secrets.
8. (Optional) Enable the seed administrator account by setting `IdentitySeed:Enabled` to `true` and providing `Email`, `Password`, and `Roles`.
9. Apply database migrations:
   ```bash
   dotnet ef database update \
     --project Identity.Base/Identity.Base.csproj \
     --startup-project Identity.Base.Host/Identity.Base.Host.csproj
   ```
10. Run the service:
   ```bash
   dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj
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

## Customising the Host Composition

`Identity.Base.Host/Program.cs` now composes the core services via the fluent builder:

```csharp
var identityBuilder = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);

identityBuilder
    .AddGoogleAuth()
    .AddMicrosoftAuth()
    .AddAppleAuth();
```

- Call `AddConfiguredExternalProviders()` to register only the providers that are enabled in configuration.
- Chain `AddExternalAuthProvider(...)` to plug in custom providers without modifying the package.
- Use `AddIdentityBase(builder.Configuration, builder.Environment, options =>
  options.ConfigureOptions((services, configuration) => { /* override binding */ }));` to load settings from external stores (e.g., database, Key Vault) before the default option binding runs. Disable the built-in JSON binding by setting `options.UseDefaultOptionBinding = false` when replacing it entirely.
- Review `docs/identity-base-public-api.md` for the supported public surface area when wiring custom services.

## Server Key Providers

OpenIddict uses configuration-driven providers to load signing and encryption certificates:

- `OpenIddict:ServerKeys:Provider`
  - `Development` *(default)* – uses development certificates (suitable only for local builds).
  - `File` – loads `.pfx` certificates from disk.
  - `AzureKeyVault` – retrieves base64-encoded PFX secrets from Azure Key Vault.

### File Provider

```json
"OpenIddict": {
  "ServerKeys": {
    "Provider": "File",
    "File": {
      "Signing": {
        "Path": "./certs/identity-signing.pfx",
        "Password": "strong-password"
      },
      "Encryption": {
        "Path": "./certs/identity-encryption.pfx",
        "Password": "strong-password"
      }
    }
  }
}
```

Only the signing certificate is required; the encryption entry is optional.

### Azure Key Vault Provider

```json
"OpenIddict": {
  "ServerKeys": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://contoso-identity.vault.azure.net/",
      "SigningSecretName": "identity-signing-cert",
      "EncryptionSecretName": "identity-encryption-cert",
      "ManagedIdentityClientId": "<optional-client-id>"
    }
  }
}
```

- Store certificates as base64-encoded PFX secrets. Optionally specify secret versions or passwords with `SigningSecretVersion`, `SigningSecretPassword`, and the corresponding encryption fields.
- The loader uses the default Azure credential chain; set `ManagedIdentityClientId` when targeting a specific managed identity.

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

## Sample Applications

### React SPA Client
- A reference SPA lives under `apps/sample-client` (Vite + React + Tailwind). It exercises registration, login + MFA, profile updates, external connectors, and the PKCE authorization code flow.
- Configure the harness by copying `.env.example` to `.env` and setting any overrides (API base URL, redirect URI, optional external providers).
- The client enables token auto-refresh by default. Pass `autoRefresh: false` to `IdentityProvider` if you prefer to manage refresh flows manually.
- Install dependencies and start the dev server:
  ```bash
  cd apps/sample-client
  npm install
  npm run dev
  ```
- The Vite server proxies API requests to `http://localhost:8080` by default. Adjust `VITE_API_BASE` or the proxy config in `vite.config.ts` if your API runs elsewhere.

### ASP.NET Core Sample API
- A minimal API example lives under `apps/sample-api` demonstrating JWT Bearer authentication using the `Identity.Base.AspNet` integration library.
- The sample API includes public endpoints, protected endpoints, and scope-based authorization examples.
- Run the sample API:
  ```bash
  cd apps/sample-api
  dotnet run --launch-profile https
  ```
- The API runs on `https://localhost:7001` and demonstrates authentication with Identity.Base tokens.
- See the [Identity.Base.AspNet README](../Identity.Base.AspNet/README.md) for complete integration details.
