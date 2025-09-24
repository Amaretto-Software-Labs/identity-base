# Changelog

## Sprint 02 – Identity Core & Registration Metadata
- Added ASP.NET Core Identity with GUID keys, strict password policy, and optional seed administrator support.
- Persisted user profile metadata as JSONB with configurable registration fields and confirmation URL template.
- Introduced `/auth/register` endpoint with FluentValidation, metadata enforcement, and outbound confirmation email dispatch.
- Implemented MailJet templated email sender using the transactional email API (always-on) and documented configuration expectations.
- Generated `InitialIdentity` EF Core migration (PascalCase tables) and updated onboarding docs, including registration guidance and getting-started primer.
- Added configuration-driven OpenIddict seeding and CORS policy management with per-environment origins.

## Sprint 03 – Authorization Code PKCE Flow
- Completed the username/password + authorization-code PKCE flow: `/auth/login` establishes the Identity cookie, `/connect/authorize` issues codes, and `/connect/token` exchanges them for tokens.
- Hardened `/connect/authorize` to emit `401 Unauthorized` with `login_required` when the SPA lacks a session, eliminating server-side login redirects.
- Added `/auth/logout` plus email-management endpoints (`/auth/confirm-email`, `/auth/resend-confirmation`, `/auth/forgot-password`, `/auth/reset-password`) and widened test coverage to include refresh-token grants, prompt=consent, and logout edge cases.
- Documented the SPA authentication steps (login → authorize → token exchange → logout) and refreshed configuration guidance for OpenIddict clients and CORS origins.

## Sprint 04 – MFA & Security Enhancements
- Introduced authenticator-based MFA endpoints (`/auth/mfa/enroll`, `/auth/mfa/verify`, `/auth/mfa/disable`, `/auth/mfa/recovery-codes`), wired into the login flow (step-up, remember-machine, recovery codes) with dedicated DI + options (`Mfa:Issuer`).
- Added configurable SMS and email MFA challenges (`Mfa:Sms`, `Mfa:Email`, MailJet `Templates.MfaChallenge`) with `/auth/mfa/challenge`, plus comprehensive integration tests covering enrollment, login step-up (authenticator/SMS/email), recovery codes, and disablement.
- Added Google, Microsoft, and Apple external sign-in support (`ExternalProviders` configuration, `/auth/external/{provider}/start`, `/auth/external/{provider}/callback`, `DELETE /auth/external/{provider}`) with linking flows and return-url redirects suitable for SPAs.
- Surfaced dynamic profile metadata via `/auth/profile-schema` and `/users/me`, and added `PUT /users/me/profile` with schema-driven validation, concurrency control, refreshed claims, and test coverage.
- Introduced structured audit logging (user/correlation enrichers, centralized `IAuditLogger`) covering MFA actions, profile updates, and external provider events, plus additional health checks for MailJet configuration and external providers.
