# Engineering Principles & Coding Standards
**Applies to:** All Amaretto Software Labs applications (API, web, mobile, admin portal, marketing site etc)

---

## 1. Core Philosophy
- **Customer value first:** every increment should deliver observable value while maintaining operational integrity.
- **Simplicity over cleverness:** prefer readable, self-documenting code with minimal abstractions that directly serve business needs.
- **Consistency:** follow shared patterns so teams can navigate any codebase confidently.

---

## 2. Backend (.NET 9 Minimal APIs)
- **Architecture:** use minimal APIs with clearly defined endpoint groups; keep controllers out of scope.
- **Language version:** use C# 13 and prefer modern features (primary constructors, collection expressions, etc.) when they improve clarity.
- **Composition:** keep `Program.cs` light by delegating service registration, middleware, and endpoint mapping to dedicated extension methods/modules; wire new features through feature folders instead of piling logic into startup files.
- **Unit of Work:** access data via the Unit of Work abstraction and repositories; avoid scattering `DbContext` across services.
- **Authorization:** rely on named policies and requirement handlers (backed by `ICurrentUserService`) instead of manual claim parsing or inline role checks; enforce access at the middleware/policy layer.
- **Extensibility Hooks:** expose multi-tenant and portal behaviour by composing the public hooks (`ConfigureAppDbContextModel`, `ConfigureIdentityRolesModel`, `ConfigureOrganizationModel`, `AfterRoleSeeding`, `AfterIdentitySeed`, `AfterOrganizationSeed`, custom `IPermissionClaimFormatter`, `AddOrganizationClaimFormatter`, `IPermissionScopeResolver`, `AddOrganizationScopeResolver`) rather than modifying OSS code paths.
- **Application Orchestration:** prefer thin services that delegate to command/query handlers or factories for complex workflows; avoid god classes that blend persistence, domain rules, and infrastructure concerns.
- **Error Handling:** surface domain and authorization failures through centralized `ProblemDetails` responses rather than ad-hoc try/catch blocks in endpoints.
- **SOLID & Clean Code:**
  - Single Responsibility for classes/methods; extract services when behavior grows beyond one concern.
  - Open/Closed by leaning on interfaces and composition rather than conditionals.
  - Interface segregation: provide narrow contracts (e.g., read-only repositories) when possible.
  - Dependency inversion: depend on abstractions; inject via DI.
- **LINQ over loops:** prefer LINQ for collection transformations/filters over explicit loops when it keeps intent clear.
- **Method/Class Size:** target methods under ~40 lines and classes under ~200 lines; refactor when boundaries blur.
- **File & Type Placement:** colocate request/response contracts and feature-specific types next to the endpoints/services that own them; shared cross-cutting concerns belong in their own namespaces (e.g., `Extensions`, `Infrastructure`, `Auditing`). Avoid leaving orphaned records or services in `Program.cs` or unrelated folders.
- **Testing:** pair every feature with unit tests + integration/service tests; mock only true boundaries (email, storage, queues).
- **Documentation:** rely on descriptive naming; add XML comments only for public APIs or complex algorithms.

---

## 3. Frontend (React / React Native / Astro)
- **Componentization:** keep UI components small (<150 lines) and single-purpose; compose them into feature modules.
- **Theming:** support light/dark themes on web and mobile using shared design tokens and user preferences.
- **State Management:** prefer hooks and TanStack Query/Zustand where appropriate; avoid prop drilling by introducing context sparingly.
- **Styling:** leverage design tokens, Tailwind/NativeWind utilities, and shared component libraries; avoid inline styles that break consistency.
- **Testing:** write unit tests for pure components and integration/E2E tests for flows (Playwright, Detox, etc.).
- **Accessibility & i18n:** ensure ARIA practices, keyboard navigation, and localization hooks are applied from the start.
- **Localisation:** ensure all strings in the application are localisable, in one place and can be translated easily when new languages are added.

---

## 4. Cross-Cutting Practices
- **Code Reviews:** enforce peer review with checklists (tests, accessibility, security, telemetry).
- **Branch Hygiene:** short-lived feature branches; rebase frequently on `main`.
- **Observability:** add telemetry (App Insights, Clarity) when introducing new flows; monitor error budgets; emit structured logs via Serilog across services.
- **Automation:** utilize CI pipelines for linting, testing, formatting, and security scanning.
- **Technical Debt:** track TODOs/issues transparently and schedule remediation sprints when debt accrues.

---

## 5. Definition of Done (per story)
- Functionality implemented following SOLID and componentization guidelines.
- Tests written/executed; CI pipeline green.
- Telemetry, logging, and alerts updated.
- Documentation (README/docs/task list) updated.
- Code reviewed and approved with no high-severity comments outstanding.

Adhering to these principles ensures maintainable, scalable, and consistent implementations across all applications.
