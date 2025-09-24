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
- Added `/auth/logout` for SPA sign-out and widened test coverage to include refresh-token grants, prompt=consent, and logout edge cases.
- Documented the SPA authentication steps (login → authorize → token exchange → logout) and refreshed configuration guidance for OpenIddict clients and CORS origins.
