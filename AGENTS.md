# AGENTS Guide

This document helps AI agents and new contributors quickly locate key references and understand the repository layout.

## Primary References
- **Project Overview:** `README.md`
- **Detailed Plan & Roadmap:** `docs/plans/identity-oidc-project-plan.md`
- **Engineering Principles:** `docs/reference/Engineering_Principles.md`
- **Database Guidelines:** `docs/reference/Database_Design_Guidelines.md`
- **Getting Started:** `docs/guides/getting-started.md`
- **MailJet Integration Guide:** `docs/guides/mailjet-email-sender.md`
- **Sprint Backlog:** `docs/sprints/`
  - [Sprint 01](docs/sprints/sprint-01.md)
  - [Sprint 02](docs/sprints/sprint-02.md)
  - [Sprint 03](docs/sprints/sprint-03.md)
  - [Sprint 04](docs/sprints/sprint-04.md)
  - [Sprint 05](docs/sprints/sprint-05.md)

## Current Focus
Check the latest sprint document to understand in-progress stories, priorities, and dependencies. Stories are organized by stream and include detailed tasks to execute without ambiguity.

## Upcoming Documentation
The following guides will be authored in later sprints:
- `docs/guides/integration-guide.md`
- `docs/guides/docker.md`

## Key Conventions
- Source code lives in project-named folders (primary service: `Identity.Base/` beneath the repo root alongside `Identity.sln`).
- Tests and infrastructure scripts are colocated with their projects following the engineering principles.
- All configuration-driven features (OpenIddict, registration metadata, MailJet) are defined in `appsettings` sections and validated via options.

## Support & Escalations
If more context is required, review the project plan and sprint docs first. For unresolved questions, contact the owning team with references to relevant documents and sections.
