# Identity.Base Release Checklist

1. **Update changelog** – ensure the "Unreleased" section captures the work included in the drop. Move it under a tagged heading when publishing.
2. **Set package version** – choose the NuGet semantic version (e.g., `1.0.0-alpha.1`). Provide it when triggering the `CI` workflow via **Run workflow**.
3. **Trigger release workflow** – from GitHub Actions, run the workflow manually with the version (and set `publish-to-nuget` to true if you want to push). The job rebuilds/tests, packs both projects, and uploads artifacts named `nuget-packages-<version>`.
4. **Smoke test the packages** – download artifacts from the workflow run, add them to a sample host application, and verify migrations/options behave as expected.
5. **Publish to NuGet (optional automation)** – set `publish-to-nuget` to true and configure `NUGET_API_KEY` secret to push automatically. Otherwise, run `dotnet nuget push` locally using the artifacts.
6. **Tag the release** – create a Git tag/release notes referencing the changelog entry.
7. **Docs** – if APIs changed, update `docs/identity-base-public-api.md` and `docs/getting-started.md` accordingly.

_Optional_: Add release automation to the CI pipeline by supplying a NuGet API key and calling `dotnet nuget push` after the pack step.
