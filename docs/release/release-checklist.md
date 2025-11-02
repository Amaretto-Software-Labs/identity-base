# Identity.Base Release Checklist

1. **Update changelog** – ensure the "Unreleased" section captures the work included in the drop. Move it under a tagged heading when publishing.
2. **Set package version** – choose the NuGet semantic version (e.g., `1.0.0-alpha.1`). Provide it when triggering the `CI` workflow via **Run workflow**.
3. **Trigger release workflow** – from GitHub Actions, run the workflow manually with the version (and set `publish-to-nuget` to true if you want to push). The job rebuilds/tests, packs all NuGet projects (`Identity.Base`, `Identity.Base.Roles`, `Identity.Base.Admin`, `Identity.Base.Organisations`, `Identity.Base.AspNet`, `Identity.Base.Email.MailJet`), uploads artifacts named `nuget-packages-<version>`, and creates a GitHub release with the same version tag.
4. **Smoke test the packages** – download artifacts from the workflow run, add them to a sample host application, and verify migrations/options behave as expected.
5. **Publish to NuGet (optional automation)** – set `publish-to-nuget` to true and configure `NUGET_API_KEY` secret to push automatically. Otherwise, run `dotnet nuget push` locally using the artifacts (the GitHub release already references them).
6. **Reconcile release notes** – the workflow creates the GitHub release entry; update its description as needed to match the changelog.
7. **Docs** – if APIs changed, update `docs/reference/identity-base-public-api.md` and `docs/guides/getting-started.md` accordingly.

_Optional_: Add release automation to the CI pipeline by supplying a NuGet API key and calling `dotnet nuget push` after the pack step.
