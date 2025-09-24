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
4. Replace the MailJet placeholders (`MailJet:ApiKey`, `MailJet:ApiSecret`, `MailJet:FromEmail`, `MailJet:Templates:Confirmation`, `MailJet:Templates:PasswordReset`) with valid values and, if you want operational alerts, enable `MailJet:ErrorReporting` with a monitored inbox. The service will fail to start without these credentials.
5. Configure OpenIddict applications/scopes under the `OpenIddict` section (client IDs, redirect URIs, permissions, resources). The default sample client targets a local SPA.
6. (Optional) Enable the seed administrator account by setting `IdentitySeed:Enabled` to `true` and providing `Email`, `Password`, and `Roles`.
7. Apply database migrations:
   ```bash
   dotnet ef database update --project Identity.Base/Identity.Base.csproj
   ```
8. Run the service:
   ```bash
   dotnet run --project Identity.Base/Identity.Base.csproj
   ```
9. Submit a registration request with metadata:
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
- MailJet integration is always on. Populate `MailJet` API credentials, sender details, confirmation template id, and (optionally) enable `MailJet:ErrorReporting` to receive delivery failures.
- When enabled, `/auth/register` sends the confirmation template with the following variables:
  - `email`
  - `displayName`
  - `confirmationUrl`

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
