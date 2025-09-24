# Sprint 04 – Security Enhancements & External Identity

## Focus & Priority
- Implement MFA enrolment flows, external identity provider sign-in, metadata management endpoints, and audit logging enhancements.
- Priority: **High** for security/auth; supporting docs/test tasks medium-high.

## Streams
- **MFA & Account Security** – Authenticator app support, recovery codes, device management.
- **External Identity Providers** – Google, Apple, Microsoft integration and lifecycle management.
- **Profile Metadata Management** – Dynamic schema exposure and profile update endpoints.
- **Observability & Audit** – Enhanced logging and alerting around auth events.

## Stories

### S4-MFA-301: Implement TOTP MFA Enrolment & Verification (Priority: High, Stream: MFA & Account Security)
**Description**
Provide endpoints and UI hooks for enabling authenticator-based MFA with recovery codes.

**Status:** Completed

**Acceptance Criteria**
- Endpoints `/auth/mfa/enroll`, `/auth/mfa/verify`, `/auth/mfa/disable`, `/auth/mfa/recovery-codes` implemented.
- Users can retrieve authenticator key + QR payload, verify code, and receive recovery codes.
- Login flow enforces MFA when enabled; tokens issued only after MFA success.

**Tasks**
- [x] Configure Identity `TotpSecurityStampBasedTokenProvider`, dedicated cookies, and register MFA services/options.
- [x] Implement minimal `/auth/mfa/*` endpoints handling enrollment, verification (including step-up), disablement, and recovery codes.
- [x] Update `/auth/login` to surface the two-factor requirement and rely on `/auth/mfa/verify` for step-up.
- [x] Add integration tests covering enrollment, login step-up, disablement, and recovery code regeneration.

**Dependencies**
- S3-AUTH-203.

### S4-MFA-302: Add Optional Secondary Factors (Email/SMS) (Priority: Medium, Stream: MFA & Account Security)
**Description**
Provide extension points for email/SMS second factors to be configured by adopters.

**Status:** Completed

**Acceptance Criteria**
- `IMfaChallengeSender` abstraction with Twilio-backed SMS implementation, email delivery via MailJet, and disabled fallback.
- Configuration toggles (`Mfa:Email`, `Mfa:Sms`) allow enabling/disabling additional factors per environment; MailJet templating covers MFA challenges.
- MFA endpoints accept `method` hints, expose `/auth/mfa/challenge` for SMS/email delivery, and documentation outlines integration steps for both.

**Tasks**
- [x] Implement `IMfaChallengeSender` with Twilio SMS support, MailJet email delivery, and DI configuration.
- [x] Update MFA endpoints and validators to support challenge/verification method selection (authenticator, sms, email, recovery).
- [x] Document SMS/email configuration and SPA usage in getting-started guide.

**Dependencies**
- S4-MFA-301.

### S4-EXT-303: Integrate Google/Apple/Microsoft Sign-In (Priority: High, Stream: External Identity Providers)
**Description**
Enable social login providers with configuration-driven setup and account linking.

**Status:** Completed

**Acceptance Criteria**
- Authentication schemes for Google, Apple (OpenIdConnect), and Microsoft configured via options binding.
- Endpoints to start callback flow (`/auth/external/{provider}/start`, `/auth/external/{provider}/callback`) implemented returning tokens via OpenIddict.
- Users can link/unlink providers from profile; metadata persisted and audited.

**Tasks**
- [x] Add provider packages and configuration classes mapping secrets/redirect URIs.
- [x] Implement external login service orchestrating sign-in, account creation (if allowed), and token issuance.
- [x] Create linking endpoints requiring existing authentication; handle duplicate account scenarios.
- [x] Add integration tests using provider stubs/fakes to simulate OAuth flows.

**Dependencies**
- S3-OIDC-201, S3-AUTH-203.

### S4-PROFILE-304: Dynamic Profile Schema & Update Endpoint (Priority: High, Stream: Profile Metadata Management)
**Description**
Expose metadata schema and allow authenticated users to update profile data with validation.

**Status:** Completed

**Acceptance Criteria**
- Endpoint `/auth/profile-schema` returns metadata field definitions (type, label, validation rules).
- Endpoint `PUT /users/me/profile` accepts metadata updates, validates per schema, persists to JSONB, and emits audit log.
- Metadata changes reflected in tokens and returned from `GET /users/me`.

**Tasks**
- [x] Implement schema DTO builder reading from `Registration:ProfileFields` configuration.
- [x] Create handler for profile updates with concurrency control (`ConcurrencyStamp`).
- [x] Update `IUserClaimsPrincipalFactory` or token pipeline to include metadata claims.
- [x] Add integration tests verifying schema endpoint and update behaviour (valid/invalid inputs, concurrency failure).

**Dependencies**
- S2-DATA-102, S2-API-103.

### S4-AUDIT-305: Enhanced Observability & Security Logging (Priority: Medium, Stream: Observability & Audit)
**Description**
Emit structured logs/metrics for security events and integrate health endpoints.

**Status:** Completed

**Acceptance Criteria**
- Audit events emitted for MFA actions, external provider link/unlink, profile updates.
- Health checks expanded to include MailJet, database, and optional external provider configuration.
- Alerts/logging guidance documented.

**Tasks**
- [x] Add Serilog enrichers for user id, correlation id, event type.
- [x] Implement audit service writing to logs (and optional future sinks) for key actions.
- [x] Update `/health` endpoint grouping and document how to monitor results.

**Dependencies**
- Dependent on relevant feature stories.
