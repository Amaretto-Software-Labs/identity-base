# Sprint 03 – OpenID Connect & Core Auth Flows

## Focus & Priority
- Configure OpenIddict, deliver authentication endpoints (login, token, email confirmation), and ensure metadata is exposed appropriately.
- Priority: **High** across streams.

## Streams
- **OIDC & Token Service** – Configure OpenIddict server/validation, client seeding, and token issuance.
- **Authentication Flows** – Implement login, confirmation, forgot/reset flows with ProblemDetails.
- **Configuration & Seeding** – Bind appsettings for clients/scopes and seed them reliably.
- **Testing & QA** – Expand integration tests covering OIDC flows.

## Stories

### S3-OIDC-201: Configure OpenIddict Server & Validation (Priority: High, Stream: OIDC & Token Service)
**Description**
Add OpenIddict server/validation components, enabling desired endpoints and grants with Identity integration.
**Status:** Completed

**Acceptance Criteria**
- OpenIddict configured for authorization code + PKCE, refresh token, and password grants (if enabled) with JWT tokens.
- Token endpoint `/connect/token` functioning with stubbed client.
- Validation configured for API endpoints using access tokens.

**Tasks**
- [x] Add OpenIddict packages and configure in service extensions (endpoints, flow options, signing/encryption keys).
- [x] Integrate ASP.NET Identity for password/refresh flows; ensure pipeline authenticates via OpenIddict validation.
- [x] Expose userinfo and introspection endpoints secured via policies.

**Dependencies**
- Sprint 2 Identity foundation.

### S3-CONFIG-202: Configuration-Driven Client/Scope Seeding (Priority: High, Stream: Configuration & Seeding)
**Description**
Bind OpenIddict clients/scopes from configuration and seed them idempotently at startup.
**Status:** Completed

**Acceptance Criteria**
- `OpenIddictOptions` classes map `OpenIddict:Applications` and `OpenIddict:Scopes` sections with validation.
- Hosted seeder service reads configuration and upserts clients/scopes using OpenIddict managers.
- Support hashed secrets and environment overrides.

**Tasks**
- [x] Implement options classes + validation ensuring required fields (clientId, client type, redirectUris).
- [x] Create `OpenIddictSeeder` hosted service performing create/update operations with logging.
- [x] Add integration test verifying seeding from in-memory configuration.

**Dependencies**
- S3-OIDC-201.

### S3-AUTH-203: Login & Auth-Code Flow Integration (Priority: High, Stream: Authentication Flows)
**Description**
Deliver `/auth/login` for email/password sign-in, establish the Identity cookie, and rely on the authorization-code-with-PKCE flow (`/connect/authorize` + `/connect/token`) for token issuance.

**Status:** Completed

**Acceptance Criteria**
- `/auth/login` validates credentials with `SignInManager`, enforces confirmed-email policy, and responds with `200 OK` while setting the Identity cookie.
- Unauthenticated `/connect/authorize` requests return `401 Unauthorized` with `WWW-Authenticate: error="login_required"`; authenticated requests issue authorization codes that exchange successfully at `/connect/token` (including refresh-token grants).
- Integration tests cover successful login/authorize/token flows, refresh token exchange, unconfirmed email rejection, and lockout handling.

**Tasks**
- [x] Implement login DTOs/validator and keep response contract SPA-friendly (success message + cookie).
- [x] Ensure OpenIddict authorize pipeline honors the Identity cookie, emits `login_required` when absent, and supports PKCE + consent prompts.
- [x] Add integration tests for happy path, refresh grant, `prompt=none`, and logout regression scenarios.

**Dependencies**
- S3-OIDC-201, S2-API-103.

### S3-AUTH-204: Email Confirmation & Password Reset Flows (Priority: High, Stream: Authentication Flows)
**Description**
Implement endpoints for email confirmation, resend confirmation, forgot password, and reset password.

**Status:** Completed

**Acceptance Criteria**
- Endpoints: `/auth/confirm-email`, `/auth/resend-confirmation`, `/auth/forgot-password`, `/auth/reset-password` are implemented with consistent responses and validation.
- Identity tokens (confirmation/reset) are generated and consumed; invalid/expired tokens return ProblemDetails without leaking account existence.
- MailJet templates dispatch confirmation and password reset emails with the required variables.

**Tasks**
- [x] Implement endpoint handlers with FluentValidation and structured error handling.
- [x] Extend MailJet options/templates to cover password reset emails and URL templates.
- [x] Add integration tests covering confirmation, resend, forgot, and reset flows using the fake email sender.

**Dependencies**
- S2-EMAIL-104.

### S3-TEST-205: Expand Integration & Security Tests (Priority: Medium, Stream: Testing & QA)
**Description**
Build out comprehensive tests for OIDC flows to protect against regressions.

**Acceptance Criteria**
- Integration suite covers register → confirm → login → token refresh path end-to-end using Testcontainers PostgreSQL.
- Security tests validate PKCE requirements, invalid client handling, and metadata claim inclusion.
- Test execution documented in README.

**Tasks**
- [ ] Configure Testcontainers-based Postgres fixture reused across tests.
- [ ] Write scenario tests chaining registration, confirmation, login, refresh.
- [ ] Document test commands and environment variables (`ASPNETCORE_ENVIRONMENT=Testing`, etc.).

**Dependencies**
- Stories S3-OIDC-201, S3-AUTH-203, S3-AUTH-204.
