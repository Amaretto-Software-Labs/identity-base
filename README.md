# Identity Service Repository

This repository contains a .NET 9-based identity and OpenID Connect service designed to be dropped into existing projects or deployed as a self-hosted auth platform. It follows Amaretto Software Labs engineering principles, supports configurable user metadata, MFA, and social login integrations, and ships with detailed documentation and sprint plans.

## Directory Structure
- `Identity.Base/` – Primary ASP.NET Core minimal API project.
- `Identity.Base/docs/` – Service-specific architecture notes, ERDs, and onboarding guides.
- `docs/` – Architecture, engineering principles, sprint plans, and integration guides.
- `docs/sprints/` – Sprint-by-sprint plans breaking down stories and tasks.
- `README.md` – This overview.
- `AGENTS.md` – Rapid orientation guide for AI agents and contributors.

## Key Documents
- [Project Plan](docs/identity-oidc-project-plan.md)
- [Engineering Principles](docs/Engineering_Principles.md)
- [Database Design Guidelines](docs/Database_Design_Guidelines.md)
- [MailJet Email Sender Guide](docs/mailjet-email-sender.md)
- Sprint Backlog:
  - [Sprint 01](docs/sprints/sprint-01.md)
  - [Sprint 02](docs/sprints/sprint-02.md)
  - [Sprint 03](docs/sprints/sprint-03.md)
  - [Sprint 04](docs/sprints/sprint-04.md)
  - [Sprint 05](docs/sprints/sprint-05.md)

## Getting Started
1. Review the [Project Plan](docs/identity-oidc-project-plan.md) for objectives, architecture guidance, and roadmap.
2. Check the relevant sprint document to understand current priorities and tasks.
3. Follow instructions in `/docs/getting-started.md` for environment setup and the SPA authentication walkthrough (login → authorize → token → logout).
4. Run `dotnet build Identity.sln` from the repository root once scaffolded.

### Local PostgreSQL Setup
- Use the default connection string found in `Identity.Base/appsettings.Development.json` (`identity/identity` credentials).
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
- MailJet integration is always active—replace the placeholder API credentials, sender details, template id, and (optionally) configure `MailJet:ErrorReporting` with a monitoring inbox before running the service.
- Configure OpenIddict clients/scopes in `OpenIddict` settings; the default seed adds a SPA sample client and `identity.api` resource scope.
- Set CORS origins in the `Cors:AllowedOrigins` array to match trusted frontends.

## Contributing
- Read [Engineering Principles](docs/Engineering_Principles.md) and [Database Design Guidelines](docs/Database_Design_Guidelines.md).
- Check the current sprint plan before starting new work.
- Documentation updates should accompany feature work.

## Contact & Support
- For auth/integration questions, consult upcoming integration docs or reach out to the owning team.
- MailJet template setup guidance lives in [mailjet-email-sender.md](docs/mailjet-email-sender.md).
