# AGENTS Guide

This document helps AI agents and new contributors quickly locate key references and understand the repository layout.

## Primary References
- **Project Overview:** `README.md`
- **Detailed Plan & Roadmap:** `docs/plans/identity-oidc-project-plan.md`
- **Engineering Principles:** `docs/reference/Engineering_Principles.md`
- **Database Guidelines:** `docs/reference/Database_Design_Guidelines.md`
- **Getting Started:** `docs/guides/getting-started.md`
- **MailJet Integration Guide:** `docs/guides/mailjet-email-sender.md`
- **Admin Operations Guide:** `docs/guides/admin-operations-guide.md`
- **Sprint Backlog:** `docs/sprints/`
  - [Sprint 01](docs/sprints/sprint-01.md)
  - [Sprint 02](docs/sprints/sprint-02.md)
  - [Sprint 03](docs/sprints/sprint-03.md)
  - [Sprint 04](docs/sprints/sprint-04.md)
  - [Sprint 05](docs/sprints/sprint-05.md)

## Current Focus
Check the latest sprint document to understand in-progress stories, priorities, and dependencies. Stories are organized by stream and include detailed tasks to execute without ambiguity.

## Upcoming Documentation
No additional guides are scheduled right now. Check the sprint backlog for future documentation efforts.

## Key Conventions
- Source code lives in project-named folders (primary service: `Identity.Base/` beneath the repo root alongside `Identity.sln`).
- Tests and infrastructure scripts are colocated with their projects following the engineering principles.
- All configuration-driven features (OpenIddict, registration metadata, MailJet) are defined in `appsettings` sections and validated via options.
- Prefer LINQ over explicit loops when it keeps the intent clear.

## Non-Negotiable Rules
- **NEVER DELETE FILES YOU DID NOT CREATE OR MODIFY IN THIS SESSION.** If you find an unexpected file or change, stop and ask for guidance instead of removing it.
- Favor minimal, well-scoped edits; leave unrelated files untouched.
- Capture assumptions and uncertainties in your hand-off notes so follow-up work is easy.

## Support & Escalations
If more context is required, review the project plan and sprint docs first. For unresolved questions, contact the owning team with references to relevant documents and sections.
