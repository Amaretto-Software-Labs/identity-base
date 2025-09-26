# Identity Service Repository

This repository contains a .NET 9-based identity and OpenID Connect service designed to be dropped into existing projects or deployed as a self-hosted auth platform. It follows Amaretto Software Labs engineering principles, supports configurable user metadata, MFA, and social login integrations, and ships with detailed documentation. The project is open source under the MIT License.

## Directory Structure
- `Identity.Base/` – Reusable Identity/OpenIddict class library.
- `Identity.Base.Host/` – Reference ASP.NET Core minimal API host for local runs and tests.
- `Identity.Base.AspNet/` – ASP.NET Core integration library for JWT authentication with Identity.Base.
- `Identity.Base/docs/` – Service-specific architecture notes, ERDs, and onboarding guides.
- `apps/sample-api/` – Sample ASP.NET Core API demonstrating Identity.Base.AspNet integration.
- `docs/` – Architecture, engineering principles, sprint plans, and integration guides.
- `docs/sprints/` – Sprint-by-sprint plans breaking down stories and tasks.
- `README.md` – This overview.
- `AGENTS.md` – Rapid orientation guide for AI agents and contributors.

## Key Documents
- [Project Plan](docs/identity-oidc-project-plan.md)
- [Engineering Principles](docs/Engineering_Principles.md)
- [Database Design Guidelines](docs/Database_Design_Guidelines.md)
- [MailJet Email Sender Guide](docs/mailjet-email-sender.md)
- [Identity.Base Public API](docs/identity-base-public-api.md)
- [Release Checklist](docs/release-checklist.md)
- Sprint Backlog:
  - [Sprint 01](docs/sprints/sprint-01.md)
  - [Sprint 02](docs/sprints/sprint-02.md)
  - [Sprint 03](docs/sprints/sprint-03.md)
  - [Sprint 04](docs/sprints/sprint-04.md)
  - [Sprint 05](docs/sprints/sprint-05.md)

## Packages

| NuGet Package | Description |
| --- | --- |
| `Identity.Base` | Core Identity/OpenIddict services, EF Core context, MFA, external providers, and DI extensions for hosting the identity server. |
| `Identity.Base.AspNet` | ASP.NET Core helpers for APIs that consume Identity.Base tokens via JWT bearer authentication. |

Install with the .NET CLI (replace `<latest>` with the published version):

```bash
dotnet add package Identity.Base --version <latest>
dotnet add package Identity.Base.AspNet --version <latest>
```

Manual release builds and NuGet artifacts can be generated via the GitHub Actions **CI** workflow (see [Release Checklist](docs/release-checklist.md)).

## Getting Started

### Identity Service
1. Review the [Project Plan](docs/identity-oidc-project-plan.md) for objectives, architecture guidance, and roadmap.
2. Check the relevant sprint document to understand current priorities and tasks.
3. Follow instructions in `/docs/getting-started.md` for environment setup, the SPA authentication walkthrough (login → authorize → token → logout), and optional MFA enrolment.
4. Run `dotnet build Identity.sln` from the repository root once scaffolded.
5. Start the reference host with `dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj` and compose providers via the fluent `AddIdentityBase` builder in `Identity.Base.Host/Program.cs`.

### ASP.NET Core Integration
The `Identity.Base.AspNet` library provides easy JWT Bearer authentication integration for ASP.NET Core APIs. See the [Identity.Base.AspNet README](Identity.Base.AspNet/README.md) for complete setup instructions and API reference. Quick example:

```csharp
// Add to Program.cs
builder.Services.AddIdentityBaseAuthentication("https://your-identity-base-url");

// Configure middleware
app.UseIdentityBaseRequestLogging();
app.UseIdentityBaseAuthentication();

// Protect endpoints
app.MapGet("/api/protected", () => "Protected data")
    .RequireAuthorization();
```

### Local PostgreSQL Setup
- Use the default connection string found in `Identity.Base.Host/appsettings.Development.json` (`identity/identity` credentials).
- Persist database objects using **PascalCase** table names. When a prefix is required, use `Identity_` (e.g., `Identity_UserProfile`).
- Quick-start with Docker Compose:

```yaml
services:
  postgres:
    image: postgres:16
    container_name: identity-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: identity
      POSTGRES_USER: identity
      POSTGRES_PASSWORD: identity
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

volumes:
  postgres-data:
```

- Update the `Primary` connection string or environment variables if your local credentials differ while preserving PascalCase table naming.
- Configure registration metadata via the `Registration` section (profile fields, confirmation URL template) and optional seed accounts under `IdentitySeed`.
- MailJet integration is always active—replace the placeholder API credentials, sender details, and template ids (`Confirmation`, `PasswordReset`, `MfaChallenge`), and (optionally) configure `MailJet:ErrorReporting` with a monitoring inbox before running the service.
- Configure OpenIddict clients/scopes in `OpenIddict` settings; the default seed adds a SPA sample client and `identity.api` resource scope.
- Set CORS origins in the `Cors:AllowedOrigins` array to match trusted frontends.

## Contributing
- Read the [Contributing Guide](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md) before opening an issue or pull request.
- Follow the Engineering Principles and Database Design Guidelines for architectural context.
- Please pair documentation updates with code changes.

## Contact & Support
- Open questions and bug reports via [GitHub Issues](https://github.com/amaretto-labs/identity-base/issues).
- For private or security-sensitive reports, email [opensource@identitybase.dev](mailto:opensource@identitybase.dev).
- MailJet template setup guidance lives in [mailjet-email-sender.md](docs/mailjet-email-sender.md).

## License

Identity Base is released under the [MIT License](LICENSE).
