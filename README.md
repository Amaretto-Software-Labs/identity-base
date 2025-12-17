[![CI](https://github.com/Amaretto-Software-Labs/identity-base/actions/workflows/ci.yml/badge.svg)](https://github.com/Amaretto-Software-Labs/identity-base/actions/workflows/ci.yml)

[![NuGet version](https://img.shields.io/nuget/v/Identity.Base?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Identity.Base)
[![NuGet downloads](https://img.shields.io/nuget/dt/Identity.Base?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/Identity.Base)
[![npm version](https://img.shields.io/npm/v/@identity-base/react-client?style=flat-square&logo=npm)](https://www.npmjs.com/package/@identity-base/react-client)
[![npm downloads](https://img.shields.io/npm/dt/@identity-base/react-client?style=flat-square&logo=npm&label=downloads)](https://www.npmjs.com/package/@identity-base/react-client)
[![npm downloads/month](https://img.shields.io/npm/dm/@identity-base/react-client?style=flat-square&logo=npm&label=downloads/month)](https://www.npmjs.com/package/@identity-base/react-client)

# Identity Base

Identity Base is a modular Identity + OpenID Connect platform for .NET 9. It packages ASP.NET Core Identity, provider-agnostic EF Core contexts, OpenIddict server setup, MFA, external providers (Google, Microsoft, Apple), optional Mailjet email delivery, and deployment-ready defaults. The recommended architecture is a dedicated Identity Host that runs all identity surfaces, a fleet of JWT-protected microservices, and a React 19 SPA consuming the APIs. Hosts are responsible for configuring the DbContexts, generating migrations for their chosen provider (PostgreSQL, SQL Server, etc.), and applying them before Identity Base runs its seeders.

The project is open source under the MIT License.

---

## Features at a Glance
- **Identity & OpenIddict orchestration** – authorization-code PKCE flow, refresh tokens, configured scopes, client seeding.
- **Multi-factor authentication** – authenticator apps, SMS, email challenges, and recovery code support.
- **External providers** – Google, Microsoft, Apple, plus fluent extension points for additional providers.
- **Mailjet email integration** – available via the optional `Identity.Base.Email.MailJet` package for confirmation, password reset, and MFA challenges.
- **Extensible DI surface** – option validators, templated email sender, MFA challenge senders, audit logging, return URL validation.
- **Secure defaults** – return URL normalization, request logging with redaction, dedicated health checks.

---

## Database Providers & Migrations
Identity Base is provider-agnostic: the packages expose DbContexts but never register a specific provider or ship migrations. Your host application must:

1. Configure each required DbContext (e.g., via the `configureDbContext` delegate on `AddIdentityBase`, `AddIdentityRoles`, `AddIdentityOrganizations`, `AddIdentityAdmin`) and choose `UseNpgsql`, `UseSqlServer`, etc.
2. Generate migrations from the host project (`dotnet ef migrations add ...`) and keep them with the host/source control.
3. Apply migrations (for example, on startup before calling `SeedIdentityRolesAsync`) and then let the Identity Base seeders run.

See `Identity.Base.Host` and `apps/org-sample-api` for reference helper extensions, design-time factories, and per-host table prefixes. The [Getting Started guide](docs/guides/getting-started.md) walks through configuring the delegates and running `dotnet ef`.

---

## Repository Overview

| Path | Purpose |
| --- | --- |
| `Identity.Base/` | Core class library (Identity, OpenIddict, EF Core contexts, MFA) published to NuGet (no bundled migrations). |
| `Identity.Base.Roles/` | Role-based access control primitives (roles, permissions, seeding helpers). |
| `Identity.Base.Admin/` | Admin API/RBAC extensions layered on the core package. |
| `Identity.Base.Organizations/` | Multi-tenant organization, membership, and role tooling. |
| `Identity.Base.AspNet/` | Helpers that let microservices validate Identity Base-issued JWTs. |
| `Identity.Base.Email.MailJet/` | Optional Mailjet email sender and configuration add-on. |
| `Identity.Base.Host/` | Opinionated ASP.NET Core host wired for local development and integration tests. Owns its migrations/prefixes and applies them before seeding on startup. |
| `apps/` | Sample APIs that demonstrate bearer auth and organization scenarios. |
| `docs/` | Architecture, engineering principles, sprint plans, onboarding, full-stack integration guides. |
| `packages/` | React client packages (`@identity-base/react-client`, `@identity-base/react-organizations`). |

Provider selection in the sample host is config-driven: set `Database:Provider` to `PostgreSql`, `SqlServer`, or `InMemory`, and optionally point `Database:Migrations:{ContextName}` (or `Database:Migrations:Default`) at provider-specific migration assemblies.

Key documents:
- [Package Documentation Hub](docs/packages/README.md)
- [Project Plan](docs/plans/identity-oidc-project-plan.md)
- [Engineering Principles](docs/reference/Engineering_Principles.md)
- [Database Design Guidelines](docs/reference/Database_Design_Guidelines.md)
- [Identity.Base Public API](docs/reference/identity-base-public-api.md)
- [Release Checklist](docs/release/release-checklist.md)
- [React Integration Guide](docs/guides/react-integration-guide.md)

### Task Playbooks
- Overview: docs/playbooks/README.md
- Pilot: docs/playbooks/identity-base-with-roles-and-organizations.md
- Manifest: docs/playbooks/index.yaml

---

## NuGet Packages

| Package | Description |
| --- | --- |
| [`Identity.Base`](https://www.nuget.org/packages/Identity.Base) | Core Identity/OpenIddict services, EF Core contexts (bring-your-own migrations), MFA, external providers, DI extensions. |
| [`Identity.Base.Roles`](https://www.nuget.org/packages/Identity.Base.Roles) | Role and permission management primitives (DbContext, seed helpers, configuration). |
| [`Identity.Base.Admin`](https://www.nuget.org/packages/Identity.Base.Admin) | Admin API extensions layered on Identity Base + roles. |
| [`Identity.Base.Organizations`](https://www.nuget.org/packages/Identity.Base.Organizations) | Organizations, memberships, and organization-scoped role tooling. |
| [`Identity.Base.AspNet`](https://www.nuget.org/packages/Identity.Base.AspNet) | ASP.NET Core helpers for microservices consuming Identity Base tokens via JWT bearer authentication. |
| [`Identity.Base.Email.MailJet`](https://www.nuget.org/packages/Identity.Base.Email.MailJet) | Optional Mailjet integration (email sender, options, health checks). |

Install via .NET CLI (replace `<latest>` with the published version):

```bash
dotnet add package Identity.Base --version <latest>
# Add-on packages as needed:
dotnet add package Identity.Base.Roles --version <latest>
dotnet add package Identity.Base.Admin --version <latest>
dotnet add package Identity.Base.Organizations --version <latest>
dotnet add package Identity.Base.AspNet --version <latest>
dotnet add package Identity.Base.Email.MailJet --version <latest>
```

Manual package builds are available through the GitHub Actions **CI** workflow (see [Release Checklist](docs/release/release-checklist.md)).

---

## Quick Start

### 1. Identity Host (all identity + admin endpoints)

```bash
dotnet restore Identity.sln
dotnet build Identity.sln
dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj
```

The host wires the full pipeline:

The host applies all bundled migrations on startup (Identity, Roles, Organizations) and seeds the admin account based on configuration. No manual `dotnet ef database update` is required unless you add custom entities.

If you install the Mailjet add-on, call `identity.UseMailJetEmailSender();` (or `builder.Services.AddMailJetEmailSender(...)`) when configuring services. Follow the [Getting Started guide](docs/guides/getting-started.md) for configuration schema, Mailjet setup, and OpenIddict application registration.

### 2. Secure .NET microservices

```csharp
// Program.cs
using Identity.Base.AspNet;

builder.Services.AddIdentityBaseAuthentication("https://identity.yourdomain.com");

var app = builder.Build();
app.UseIdentityBaseRequestLogging();
app.UseIdentityBaseAuthentication();

app.MapGet("/api/protected", () => "Secure content")
   .RequireAuthorization(policy => policy.RequireScope("identity.api"));
```

See [Identity.Base.AspNet/README.md](Identity.Base.AspNet/README.md) for detailed options, scope helpers, and troubleshooting.

### 3. React 19 SPA

Install the published React packages and wrap your app with both providers:

```bash
npm install @identity-base/react-client @identity-base/react-organizations
```

```tsx
import { IdentityProvider } from '@identity-base/react-client';
import { OrganizationsProvider } from '@identity-base/react-organizations';

<IdentityProvider config={identityConfig}>
  <OrganizationsProvider apiBase={identityConfig.apiBase}>
    <App />
  </OrganizationsProvider>
</IdentityProvider>
```

The hooks exposed by the packages (`useLogin`, `useOrganizations`, `useOrganizationMembers`, etc.) orchestrate the full identity and organization flows. The [Full Stack Integration Guide](docs/guides/full-stack-integration-guide.md) walks through setting up the Identity Host, microservices, and the SPA end-to-end.

---

## Running the Stack Locally

### Prerequisites
- .NET 9 SDK
- PostgreSQL 16 (local or Docker)
- Optional: Mailjet credentials (or MailHog for local stubbing)
- Node.js 20 / npm 10 if you run the React clients

### Configuration snapshot

### Configuration snapshot
- `Registration` – profile fields, confirmation/reset URL templates (embed `{token}` + `{userId}`).
- `MailJet` – API keys, sender info, template IDs (confirmation/reset/MFA). Only required when the Mailjet package is enabled.
- `Mfa` – issuer name, email/SMS toggles, Twilio credentials (if SMS is enabled).
- `ExternalProviders` – Google/Microsoft/Apple client IDs, secrets, scopes, callback paths.
- `OpenIddict` – client applications, scopes, server key provider (development, file-system, Azure Key Vault).
- `Cors` – allowed origins for browser clients.

Full option reference lives in [docs/guides/getting-started.md](docs/guides/getting-started.md). For the full architecture walk-through (Identity Host + microservices + React 19), see [docs/guides/full-stack-integration-guide.md](docs/guides/full-stack-integration-guide.md).

---

## Testing & Tooling
- Run `dotnet test Identity.sln` (integration + unit suites) before submitting changes.
- The host project uses EF Core InMemory for tests; design-time factory enables CLI tooling.
- CI (GitHub Actions) builds, tests, and packs both packages for every push/PR. Manual releases are triggered via **Run workflow** with a semantic version.

---

## Release & Distribution
1. Update the changelog’s “Unreleased” section.
2. Trigger the GitHub Actions workflow with the desired `package-version` (and optional `publish-to-nuget` flag). Artifacts named `nuget-packages-<version>` are produced.
3. Smoke test the packages locally before pushing to NuGet.
4. Tag the release and publish notes referencing the changelog.

Process details: [Release Checklist](docs/release/release-checklist.md). The Identity Host and sample APIs own their migrations and attempt to apply them at startup; ensure deployments allow those host-defined migrations to run (or execute them via your existing release automation) before Identity Base seeders kick in.

---

## Support
- File issues and feature requests on [GitHub](https://github.com/Amaretto-Software-Labs/identity-base/issues).
- For security or conduct-related concerns, email [opensource@amarettosoftware.com](mailto:opensource@amarettosoftware.com).
- Configuration guidance for email lives in [docs/guides/mailjet-email-sender.md](docs/guides/mailjet-email-sender.md).

---

## Contributing
- Review the [Contributing Guide](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).
- Follow the Engineering Principles and Database Design Guidelines when proposing changes.
- Pair documentation updates with functional changes.

---

## License

Identity Base is licensed under the [MIT License](LICENSE).

## Sponsors & Contributors

<p align="center">
  <a href="https://vasoftware.co.uk" target="_blank" rel="noopener" style="margin-right:32px;">
    <img src="docs/assets/VA_Logo_Horizontal_RGBx320.png" alt="VA Software Solutions" width="220" />
  </a>
  <a href="https://amarettosoftware.com" target="_blank" rel="noopener">
    <img src="docs/assets/amaretto-software-labs-logo.png" alt="Amaretto Software Labs" width="220" />
  </a>
</p>

We are grateful to VA Software Solutions and Amaretto Software Labs for supporting the Identity Base project.
