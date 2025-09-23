# Changelog

## Sprint 02 – Identity Core & Registration Metadata
- Added ASP.NET Core Identity with GUID keys, strict password policy, and optional seed administrator support.
- Persisted user profile metadata as JSONB with configurable registration fields and confirmation URL template.
- Introduced `/auth/register` endpoint with FluentValidation, metadata enforcement, and outbound confirmation email dispatch.
- Implemented MailJet templated email sender using the transactional email API (always-on) and documented configuration expectations.
- Generated `InitialIdentity` EF Core migration (PascalCase tables) and updated onboarding docs, including registration guidance and getting-started primer.
