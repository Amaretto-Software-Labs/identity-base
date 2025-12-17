# Package Documentation Hub

This directory contains canonical documentation for every Identity Base package published to NuGet or npm. Each package page follows a shared structure covering:

- Overview and core concepts
- Installation and service wiring
- Configuration options
- Public endpoints/APIs exposed by the package
- Extension points and overriding interfaces
- Dependency expectations and version alignment
- References to sample integrations and guides

Use the links below to jump directly to the package you are integrating.

| Package | Type | Docs |
| --- | --- | --- |
| `Identity.Base` | NuGet | [Core Identity Server](identity-base/index.md) |
| `Identity.Base.AspNet` | NuGet | [JWT Consumer Helpers](identity-base-aspnet/index.md) |
| `Identity.Base.Roles` | NuGet | [Role & Permission System](identity-base-roles/index.md) |
| `Identity.Base.Organizations` | NuGet | [Organizations & Memberships](identity-base-organizations/index.md) |
| `Identity.Base.Admin` | NuGet | [Admin API Surface](identity-base-admin/index.md) |
| `Identity.Base.Email.MailJet` | NuGet | [Mailjet Email Sender](identity-base-email-mailjet/index.md) |
| `@identity-base/client-core` | npm | [Client Core](identity-base-client-core/index.md) |
| `@identity-base/react-client` | npm | [React Auth Client](identity-base-react-client/index.md) |
| `@identity-base/react-organizations` | npm | [React Organizations Add-on](identity-base-react-organizations/index.md) |
| `@identity-base/angular-client` | npm | [Angular Auth Client](identity-base-angular-client/index.md) |
| `@identity-base/angular-organizations` | npm | [Angular Organizations Add-on](identity-base-angular-organizations/index.md) |

> **Maintenance note:** when a package’s public surface area changes (new endpoints, options, or extension points), update the corresponding package page as part of the pull request.

---

## Task Playbooks

Agent-friendly runbooks for common tasks live under `docs/playbooks/`.
- Start here: docs/playbooks/README.md
- Pilot playbook: docs/playbooks/identity-base-with-roles-and-organizations.md
- Manifest (machine-parsable): docs/playbooks/index.yaml
