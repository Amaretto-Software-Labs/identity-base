# Changelog

## [0.4.0] - 2025-11-01
### Highlights
- Renamed all Organisation packages, namespaces, endpoints, and React components to use British spelling.
- Modularized Identity Base into reusable libraries and ASP.NET host, adding builder APIs (`AddIdentityBase`, external provider helpers) and EF support.
- Delivered complete auth surface: registration metadata, email confirmation/reset flows, MFA (authenticator, SMS/email via Twilio/Mailjet), external providers, and authorization code PKCE.
- Introduced RBAC (`Identity.Base.Roles`), admin APIs (`Identity.Base.Admin`), and multi-tenant organisation management package (`Identity.Base.Organisations`).
- Added optional Mailjet email sender (`Identity.Base.Email.MailJet`), release automation, and refreshed documentation (getting started, full-stack guide, React integration).
- Shipped Docker/docker-compose environment, sample React client harness, and documentation covering deployment, admin operations, headless React integration, and multi-tenant planning.

## Unreleased
- _No changes yet._
