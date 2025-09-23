# Identity Service Repository

This repository contains a .NET 9-based identity and OpenID Connect service designed to be dropped into existing projects or deployed as a self-hosted auth platform. It follows Amaretto Software Labs engineering principles, supports configurable user metadata, MFA, and social login integrations, and ships with detailed documentation and sprint plans.

## Directory Structure
- `apps/` – Application source (API, future sample clients).
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
3. Follow instructions in upcoming `/docs/getting-started.md` and `/docs/integration-guide.md` (to be authored in Sprint 05).
4. Run `dotnet build` under `apps/api/src` once scaffolded.

## Contributing
- Read [Engineering Principles](docs/Engineering_Principles.md) and [Database Design Guidelines](docs/Database_Design_Guidelines.md).
- Check the current sprint plan before starting new work.
- Documentation updates should accompany feature work.

## Contact & Support
- For auth/integration questions, consult upcoming integration docs or reach out to the owning team.
- MailJet template setup guidance lives in [mailjet-email-sender.md](docs/mailjet-email-sender.md).
