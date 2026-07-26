# Identity.Base Release Checklist

1. **Update changelog** – ensure the "Unreleased" section captures the work included in the drop. Move it under a tagged heading when publishing.
2. **Set package version** – choose the NuGet semantic version (e.g., `1.0.0-alpha.1`). Provide it when triggering the `CI` workflow via **Run workflow**.
3. **Trigger release workflow** – from GitHub Actions, run the workflow manually with the version. The job rebuilds/tests and packs all NuGet projects (`Identity.Base`, `Identity.Base.Roles`, `Identity.Base.Admin`, `Identity.Base.Organizations`, `Identity.Base.ServicePrincipals`, `Identity.Base.AspNet`, `Identity.Base.Email.MailJet`, `Identity.Base.Email.SendGrid`), builds all five npm packages, uploads versioned artifacts, and creates a GitHub release with the same version tag.
4. **Smoke test the packages** – download artifacts from the workflow run, add them to a sample host application, and verify migrations/options behave as expected. Service-principal validation must include both its dedicated context migration and the updated roles-context migration.
5. **Publish to NuGet (optional automation)** – set `publish-to-nuget` to true and configure `NUGET_API_KEY` secret to push automatically. Otherwise, run `dotnet nuget push` locally using the artifacts (the GitHub release already references them).
6. **Reconcile release notes** – the workflow creates the GitHub release entry; update its description as needed to match the changelog.
7. **Publish to npm (optional automation)** – set `publish-to-npm` to true. npm publication uses trusted publishing/OIDC with provenance, so the repository/environment must be configured at npm and the workflow must retain `id-token: write`.
8. **Docs** – if APIs changed, update the package hub, public API, getting-started guide, operational playbooks, and changelog accordingly.

See also
- Task Playbook: docs/playbooks/database-migrations-and-rollback.md

NuGet publishing requires `NUGET_API_KEY`. npm publishing uses OIDC trusted publishing and does not consume an npm token in the current workflow.
