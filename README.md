# Identity Base

Identity Base is a modular Identity + OpenID Connect platform for .NET 9. It packages ASP.NET Core Identity, EF Core migrations, OpenIddict server setup, MFA, external providers (Google, Microsoft, Apple), MailJet-powered email flows, and deployment-ready defaults. Hosts can self-run the identity server or embed its capabilities through NuGet packages.

The project is open source under the MIT License.

---

## Features at a Glance
- **Identity & OpenIddict orchestration** – password + authorization-code PKCE flow, refresh tokens, configured scopes, client seeding.
- **Multi-factor authentication** – authenticator apps, SMS, email challenges, and recovery code support.
- **External providers** – Google, Microsoft, Apple, plus fluent extension points for additional providers.
- **MailJet email integration** – confirmation, password reset, MFA challenge templates, error reporting.
- **Extensible DI surface** – option validators, templated email sender, MFA challenge senders, audit logging, return URL validation.
- **Secure defaults** – password grant gating, return URL normalization, request logging with redaction, dedicated health checks.

---

## Repository Overview

| Path | Purpose |
| --- | --- |
| `Identity.Base/` | Core class library (Identity, OpenIddict, EF Core, MFA, email) packaged for NuGet. |
| `Identity.Base.Host/` | Minimal ASP.NET Core host that composes the library for local runs, migrations, and integration tests. |
| `Identity.Base.AspNet/` | Optional NuGet package to simplify JWT bearer authentication for downstream APIs. |
| `Identity.Base.Tests/` | Integration and feature tests (xUnit + WebApplicationFactory). |
| `docs/` | Architecture, engineering principles, sprint plans, onboarding, configuration guides. |
| `apps/` | Sample applications, including a JWT-consuming API. |

Key documents:
- [Project Plan](docs/plans/identity-oidc-project-plan.md)
- [Engineering Principles](docs/reference/Engineering_Principles.md)
- [Database Design Guidelines](docs/reference/Database_Design_Guidelines.md)
- [Identity.Base Public API](docs/reference/identity-base-public-api.md)
- [Release Checklist](docs/release/release-checklist.md)

---

## NuGet Packages

| Package | Description |
| --- | --- |
| [`Identity.Base`](https://www.nuget.org/packages/Identity.Base) | Core Identity/OpenIddict services, EF Core context & migrations, MFA, external providers, DI extensions. |
| [`Identity.Base.AspNet`](https://www.nuget.org/packages/Identity.Base.AspNet) | ASP.NET Core helpers for APIs consuming Identity Base tokens via JWT bearer authentication. |

Install via .NET CLI (replace `<latest>` with the published version):

```bash
dotnet add package Identity.Base --version <latest>
dotnet add package Identity.Base.AspNet --version <latest>
```

Manual package builds are available through the GitHub Actions **CI** workflow (see [Release Checklist](docs/release/release-checklist.md)).

---

## Quick Start

### 1. Self-host the Identity server

```bash
dotnet restore Identity.sln
dotnet build Identity.sln
dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj
```

The host wires the full pipeline:

```csharp
var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);

identity
    .AddConfiguredExternalProviders() // Google/Microsoft/Apple based on configuration
    .AddExternalAuthProvider("github", auth =>
        auth.AddOAuth("GitHub", options => { /* custom provider */ }));

var app = builder.Build();
app.UseApiPipeline();
app.MapControllers();
app.MapApiEndpoints();
app.Run();
```

Follow the [Getting Started guide](docs/guides/getting-started.md) for database setup, MailJet configuration, MFA, and OpenIddict seeding.

### 2. Integrate with an existing API

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

Refer to the [Identity.Base.AspNet README](Identity.Base.AspNet/README.md) for advanced configuration, scope handling, and troubleshooting.

---

## Running the Stack Locally

### Prerequisites
- .NET 9 SDK
- PostgreSQL 16 (local or Docker)
- MailJet credentials for outbound email (required for runtime startup)

### Database & migrations

```bash
dotnet ef database update \
  --project Identity.Base/Identity.Base.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj
```

Connection strings live under `ConnectionStrings:Primary`. In development we default to `identity/identity` credentials.

### Configuration snapshot
- `Registration` – profile fields, confirmation/reset URL templates.
- `MailJet` – API keys, sender info, template IDs (confirmation/reset/MFA).
- `Mfa` – issuer name, email/SMS toggles, Twilio credentials (if SMS is enabled).
- `ExternalProviders` – Google/Microsoft/Apple client IDs, secrets, scopes, callback paths.
- `OpenIddict` – client applications, scopes, server key provider (development, file-system, Azure Key Vault).
- `Cors` – allowed origins for browser clients.

Full option reference is documented in [docs/guides/getting-started.md](docs/guides/getting-started.md).

---

## Testing & Tooling
- Run `dotnet test Identity.sln` (44 integration tests) before submitting changes.
- The host project uses EF Core InMemory for tests; design-time factory enables CLI tooling.
- CI (GitHub Actions) builds, tests, and packs both packages for every push/PR. Manual releases are triggered via **Run workflow** with a semantic version.

---

## Release & Distribution
1. Update the changelog’s “Unreleased” section.
2. Trigger the GitHub Actions workflow with the desired `package-version` (and optional `publish-to-nuget` flag). Artifacts named `nuget-packages-<version>` are produced.
3. Smoke test the packages locally before pushing to NuGet.
4. Tag the release and publish notes referencing the changelog.

Process details: [Release Checklist](docs/release/release-checklist.md).

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
  <a href="https://vasoftware.co.uk" target="_blank" style="margin-right:24px;">
    <img src="docs/assets/VA_Logo_Horizontal_RGBx320.png" alt="VA Software Solutions" width="200" />
  </a>
  <a href="https://amarettosoftware.com" target="_blank">
    <img src="docs/assets/amaretto-software-labs-logo.png" alt="Amaretto Software Labs" width="200" />
  </a>
</p>

We are grateful to VA Software Solutions and Amaretto Software Labs for supporting the Identity Base project.
