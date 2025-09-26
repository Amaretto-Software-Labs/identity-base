# Identity Service Remediation Plan

**Date:** 2025-09-26  
**Prepared by:** Codex (GPT-5)

## Objectives
- Close high/medium risk security findings from the September 2025 audit.  
- Reduce exposure of sensitive data through logging and default middleware behaviour.  
- Improve maintainability by decomposing oversized services and DI bootstrap code.  
- Align OpenIddict behaviour and seed data with intended client capabilities.  
- Back new changes with automated tests and documentation updates.

## Workstreams Overview
1. External authentication hardening.  
2. Logging and observability safeguards.  
3. Authentication flow tightening (ROPC + scope handling).  
4. Dependency injection and service refactoring.  
5. OpenIddict seeding adjustments.  
6. Documentation, testing, and rollout support.

---

## 1. External Authentication Hardening
**Owner:** Identity API team  
**Goal:** Eliminate open-redirect risk and enforce deterministic callback construction.

- **Task 1.1 – Sanitize `returnUrl` handling**  
  - Update `ExternalAuthenticationService.IsRelativeUrl` and response builders to accept only single-slash relative paths; reject protocol-relative or absolute URLs.  
  - Files: `Identity.Base/Features/Authentication/External/ExternalAuthenticationService.cs`, related validators if introduced.  
  - Tests: expand `Identity.Base.Tests/ExternalAuthenticationTests.cs` with malicious `returnUrl` permutations.

- **Task 1.2 – Harden forwarded-header usage**  
  - Option A: configure `UseForwardedHeaders` in the hosting pipeline with explicit known proxies/network ranges.  
  - Option B: strip `X-Forwarded-*` reliance from `BuildCallbackUri` and document that reverse proxies must terminate SSL.  
  - Files: `Identity.Base/Extensions/ApplicationBuilderExtensions.cs` or `Program.cs`; `ExternalAuthenticationService` helper.  
  - Tests: integration-style unit tests verifying callback host/scheme behaviour.

- **Task 1.3 – Update deployment guidance**  
  - Document new return URL policy and proxy requirements.  
  - Files: `docs/guides/getting-started.md`, `docs/plans/identity-oidc-project-plan.md`, new release notes entry.

## 2. Logging & Observability Safeguards
**Owner:** Platform Foundation team  
**Goal:** Prevent leakage of PII/secrets via structured logs.

- **Task 2.1 – Centralised redaction utilities**  
  - Implement helper (e.g., `ILogSanitizer`) to obfuscate emails, phone numbers, and tokens before logging.  
  - Apply to `PasswordGrantHandler`, `AccountEmailService`, `TwilioMfaChallengeSender`, and similar code paths.  
  - Tests: unit tests around sanitiser behaviour.

- **Task 2.2 – Adjust log levels**  
  - Downgrade sensitive logs to `Debug`/`Trace` where business value is low.  
  - Ensure failure logs omit secrets while preserving diagnostics.

- **Task 2.3 – Safe defaults for ASP.NET integration**  
  - Wrap claim-dump logic in `Identity.Base.AspNet` with an environment guard or configuration flag defaulting to off.  
  - Update `Identity.Base.AspNet/README.md` to document the toggle.  
  - Tests: verify middleware honours configuration.

- **Task 2.4 – Serilog enrichment review**  
  - Confirm correlation/user IDs remain while redacting emails/phone numbers.  
  - Add regression tests or snapshots if feasible.

## 3. Authentication Flow Tightening
**Owner:** Identity Base API feature team  
**Goal:** Ensure authentication endpoints reflect intended contract and reduce attack surface.

- **Task 3.1 – Gate password grant flow**  
  - Introduce configuration flag or client metadata to enable Resource Owner Password Credentials (ROPC) selectively.  
  - Adjust `ServiceCollectionExtensions.AddIdentityBase` and OpenIddict configuration to respect the flag.  
  - Tests: scenarios covering flagged/flagless clients; update `Identity.Base.Tests/OpenIddictSeedingTests.cs`.

- **Task 3.2 – Honour requested scopes on login**  
  - Decide on scope handling for `LoginEndpoint` (honour request vs. remove property).  
  - If honouring: propagate to sign-in principal; ensure scopes are validated against client permissions.  
  - Tests: update `LoginEndpointTests` to assert scope behaviour.

- **Task 3.3 – Documentation alignment**  
  - Reflect new configuration knobs in `docs/plans/identity-oidc-project-plan.md` and sample usage guides.

## 4. Dependency Injection & Service Refactor
**Owner:** Architecture working group  
**Goal:** Improve SOLID alignment and testability.

- **Task 4.1 – Evaluate `IdentityBaseBuilder` surface**  
  - Break out focused helpers (e.g., `AddIdentityCoreServices`, `AddExternalAuthentication`, `AddOpenIddictServer`) if the fluent builder continues to grow.  
  - Ensure unit tests cover each segment; consider introducing smoke tests for DI container configuration.

- **Task 4.2 – Refactor `ExternalAuthenticationService`**  
  - Extract responsibilities (return-url validation, provider dispatch, account provisioning) into smaller classes or strategy interfaces.  
  - Expand existing unit tests or add new ones for each collaborator.

- **Task 4.3 – MFA orchestration abstraction**  
  - Move business logic from `MfaEndpoints` into an `IMfaManager` or similar service; keep endpoints thin.  
  - Tests: extend `MfaEndpointsTests`, add service-level tests.

## 5. OpenIddict Seeding Adjustments
**Owner:** Identity Platform services  
**Goal:** Ensure seeded clients/scopes reflect configuration exactly.

- **Task 5.1 – Remove hardcoded scope additions**  
  - Update `OpenIddictSeeder` to respect configured scopes without forcing `identity.api`.  
  - Tests: adjust `OpenIddictSeedingTests` to cover clients without optional scopes.

- **Task 5.2 – Configuration validation tweaks**  
  - Ensure `OpenIddictOptionsValidator` still enforces necessary defaults after changes.  
  - Update documentation with new expectations for configuration completeness.

## 6. Documentation, Testing, and Rollout
**Owner:** Shared between teams above  
**Goal:** Deliver confidence in changes and prepare stakeholders.

- **Task 6.1 – Update changelog and audit docs**  
  - Add entries to `CHANGELOG.md`, `docs/audits/audit-findings.md`, and `docs/audits/identity-code-audit.md` summarising remediation.

- **Task 6.2 – Expand automated testing**  
  - Ensure new unit/integration tests cover key paths (external auth, logging toggles, MFA orchestrator).  
  - Evaluate need for end-to-end tests via sample API or Postman collections.

- **Task 6.3 – Release readiness checklist**  
  - Verify infrastructure changes (e.g., forwarded headers, logging config) in staging.  
  - Prepare rollback and monitoring plan; communicate changes to consuming teams.

---

## Milestones & Timeline (Suggested)
1. **Week 1:** External auth hardening, logging guardrails, documentation updates.  
2. **Week 2:** Authentication flow tightening, begin DI refactors, add tests.  
3. **Week 3:** Complete refactors, adjust OpenIddict seeding, finish test suite.  
4. **Week 4:** Regression testing, staging validation, prepare release notes.

## Risks & Mitigations
- **Scope creep:** Prioritise security fixes before structural refactors; time-box large refactors.  
- **Behavioural regressions:** Leverage feature flags/config toggles to roll out gradually.  
- **Infrastructure dependencies:** Coordinate forwarded-header changes with DevOps to avoid routing issues.

## Success Criteria
- All security findings from the audit closed with code reviews and tests.  
- Logging in production no longer exposes emails, phone numbers, or tokens.  
- DI bootstrapper and services exhibit reduced cyclomatic complexity and improved test coverage.  
- OpenIddict seeding matches configuration without implicit scope grants.  
- Updated documentation reflects new behaviours and deployment requirements.
