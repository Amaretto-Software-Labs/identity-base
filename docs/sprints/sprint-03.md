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

**Acceptance Criteria**
- OpenIddict configured for authorization code + PKCE, refresh token, and password grants (if enabled) with JWT tokens.
- Token endpoint `/connect/token` functioning with stubbed client.
- Validation configured for API endpoints using access tokens.

**Tasks**
- [ ] Add OpenIddict packages and configure in `AddIdentityOidc` extension (endpoints, flow options, signing/encryption keys).
- [ ] Integrate ASP.NET Identity for password/refresh flows; ensure Data Protection keys configured for dev.
- [ ] Expose userinfo and introspection endpoints secured via policies.

**Dependencies**
- Sprint 2 Identity foundation.

### S3-CONFIG-202: Configuration-Driven Client/Scope Seeding (Priority: High, Stream: Configuration & Seeding)
**Description**
Bind OpenIddict clients/scopes from configuration and seed them idempotently at startup.

**Acceptance Criteria**
- `OpenIddictOptions` classes map `OpenIddict:Applications` and `OpenIddict:Scopes` sections with validation.
- Hosted seeder service reads configuration and upserts clients/scopes using OpenIddict managers.
- Support hashed secrets and environment overrides.

**Tasks**
- [ ] Implement options classes + validation attributes ensuring required fields (clientId, type, redirectUris).
- [ ] Create `OpenIddictSeeder` hosted service performing create/update operations with logging.
- [ ] Add integration test verifying seeding from in-memory configuration.

**Dependencies**
- S3-OIDC-201.

### S3-AUTH-203: Login & Token Issuance Endpoint (Priority: High, Stream: Authentication Flows)
**Description**
Deliver `/auth/login` supporting email/password login, metadata projection to claims, and token issuance via OpenIddict.

**Acceptance Criteria**
- Endpoint validates credentials using `SignInManager`; enforces confirmed email requirement.
- Successful login returns tokens (access, refresh) issued via OpenIddict; includes metadata claims when configured.
- Failed login returns ProblemDetails with lockout/invalid reasons; audit event logged.

**Tasks**
- [ ] Implement login request/response DTOs and validators.
- [ ] Add service orchestrating sign-in, metadata enrichment, and token issuance (via OpenIddict interaction manager).
- [ ] Write integration tests covering success, invalid credentials, unconfirmed email, and locked-out user cases.

**Dependencies**
- S3-OIDC-201, S2-API-103.

### S3-AUTH-204: Email Confirmation & Password Reset Flows (Priority: High, Stream: Authentication Flows)
**Description**
Implement endpoints for email confirmation, resend confirmation, forgot password, and reset password.

**Acceptance Criteria**
- Endpoints: `/auth/confirm-email`, `/auth/resend-confirmation`, `/auth/forgot-password`, `/auth/reset-password` implemented with consistent responses.
- Tokens generated via Identity token providers; invalid/expired tokens return ProblemDetails.
- Emails triggered using MailJet templates with appropriate variable payloads.

**Tasks**
- [ ] Implement command handlers for each endpoint with validation + logging.
- [ ] Update MailJet sender usage to include confirmation/reset templates and variables.
- [ ] Add integration tests verifying token validation and email invocation (mocked).

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
