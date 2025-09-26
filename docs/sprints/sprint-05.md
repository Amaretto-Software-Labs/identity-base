# Sprint 05 – Deployment, Harness, and Release Readiness

## Focus & Priority
- Deliver Docker-based deployment assets, React/Tailwind test harness, comprehensive testing, and open-source packaging.
- Priority: **High** for deployment/harness; Medium for ancillary docs.

## Streams
- **Deployment & DevOps** – Dockerfile, compose samples, environment configuration.
- **Sample Client & QA** – React/Tailwind harness with automated tests.
- **Documentation & Open Source** – Finalize guides, licensing, contribution workflows.
- **Hardening & Verification** – End-to-end smoke tests, load testing, release prep.

## Stories

### S5-DEP-401: Dockerize API & Provide Compose Samples (Priority: High, Stream: Deployment & DevOps)
**Description**
Create production-ready Dockerfile and docker-compose assets enabling one-command startup.

**Status:** In Progress
**Notes:** Dockerfile, compose stack, and docs are live; CI still lacks the container build + smoke test required by the acceptance criteria.

**Acceptance Criteria**
- Multi-stage Dockerfile built on .NET 9 image with non-root runtime user.
- `docker-compose.local.yml` runs API + Postgres + optional MailJet stub; environment variables documented.
- Health checks exposed for container orchestrators and readiness endpoints documented.

**Tasks**
- [ ] Author Dockerfile with build/test/publish stages, caching for NuGet packages, and environment variable defaults.
- [ ] Create compose file including Postgres volume, secrets via `.env`, and instructions for TLS proxying.
- [ ] Update docs with container build/run commands and troubleshooting tips.
- [ ] Add CI step to build container image and run basic smoke test.

**Dependencies**
- Earlier sprints delivering API features.

### S5-HARNESS-402: Build React/Tailwind Test Harness (Priority: High, Stream: Sample Client & QA)
**Description**
Deliver sample web app demonstrating registration, login (with MFA/social), profile updates, and metadata capture.

**Status:** Completed

**Acceptance Criteria**
- Vite-based React app scaffolded under `/apps/sample-client` with Tailwind configured.
- Pages: Registration (dynamic fields), Login, MFA Verification, Profile, External Login connectors.
- Harness configurable via `.env` (issuer URL, clientId, redirect URIs) and uses PKCE authorization code flow.

**Tasks**
- [x] Scaffold React app with TypeScript, Tailwind, React Router, Axios helpers.
- [x] Implement dynamic form renderer consuming `/auth/profile-schema` for registration & profile forms.
- [x] Implement PKCE helpers and token storage (secure httpOnly cookie or memory + refresh).
- [x] Provide reusable hook for checking MFA-required responses and orchestrating second factor challenge.
- [x] Document harness setup in `/docs/guides/integration-guide.md`.

**Dependencies**
- Sprint 3 & 4 auth flows complete.

### S5-QA-403: Automated E2E & Smoke Tests (Priority: High, Stream: Sample Client & QA)
**Description**
Add Cypress/Playwright suites covering password, MFA, and social login flows end-to-end.

**Status:** Not Started
**Notes:** No Playwright/Cypress dependencies or test suites exist; CI has no E2E stage yet.

**Acceptance Criteria**
- Test suite runs locally and in CI against docker-compose environment.
- Coverage includes register → confirm → login → MFA challenge → profile update → external login link/unlink.
- Test reports stored in artifacts; failures block release.

**Tasks**
- [ ] Configure Playwright (preferred for cross-browser) with fixtures pointing to docker-compose stack.
- [ ] Implement tests for each major journey including metadata validation and recovery code usage.
- [ ] Integrate tests into CI pipeline with ability to run headless in container.

**Dependencies**
- S5-HARNESS-402, API stories.

### S5-DOCS-404: Finalize Documentation & Open Source Assets (Priority: Medium, Stream: Documentation & Open Source)
**Description**
Prepare final documentation set for internal and external audiences.

**Status:** Not Started
**Notes:** LICENSE, CODE_OF_CONDUCT, release notes, and Postman collection remain unpublished.

**Acceptance Criteria**
- `/docs/guides/getting-started.md`, `/docs/guides/integration-guide.md`, `/docs/guides/docker.md` completed with step-by-step instructions and screenshots where helpful.
- `LICENSE`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, and issue/PR templates published.
- Change log updated with Sprint 05 features and release notes draft.

**Tasks**
- [ ] Complete docs with configuration tables, MFA/external provider setup steps, and compose instructions.
- [ ] Add sample Postman collection/export referencing endpoints.
- [ ] Draft release notes summarizing features, upgrade notes, and known limitations.

**Dependencies**
- Completion of prior stories for accurate documentation.

### S5-HARDEN-405: Performance, Security, and Release Checklist (Priority: Medium, Stream: Hardening & Verification)
**Description**
Run final hardening pass and ensure release checklist is satisfied.

**Status:** Not Started
**Notes:** No load-test scripts, dependency audit output, or release checklist evidence has been captured.

**Acceptance Criteria**
- Load test baseline executed (e.g., k6) covering token issuance and login flows.
- Security review checklist completed (dependency audit, secret scanning, MFA/external provider validation).
- Release checklist items (custom metadata, MFA, external providers) marked complete with evidence in docs.

**Tasks**
- [ ] Author k6 (or similar) script simulating concurrent login/refresh flows; record metrics.
- [ ] Run `dotnet list package --outdated` and dependency vulnerability scanners; document remediation.
- [ ] Verify checklist items in plan, capture evidence/links in release notes.

**Dependencies**
- All major functionality completed.
