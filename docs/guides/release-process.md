# Release Process

This repository uses [Nerdbank.GitVersioning (NBGV)](https://github.com/dotnet/Nerdbank.GitVersioning) to generate deterministic SemVer across all NuGet and npm artifacts. The GitHub Actions workflow (`.github/workflows/ci.yml`) drives both validation and publishing.

## Versioning

- `version.json` defines the base version and treats the `main` branch (and tags matching `v*.*.*`) as public releases.
- CI packs without overriding `PackageVersion`; MSBuild picks up the version emitted by NBGV.
- The workflow installs the `nbgv` CLI to expose the calculated version (via `nbgv get-version -v SemVer2`) for artifact naming and npm publishes.

## Validation (push / pull request)

1. `build` job restores, builds, and tests the solution on every PR and `main` push.
2. When running on `main`, the job also:
   - Computes the current SemVer using `nbgv`.
   - Packs the NuGet projects (`Identity.Base`, `Identity.Base.Roles`, `Identity.Base.Admin`, `Identity.Base.AspNet`, `Identity.Base.Organizations`) with `ContinuousIntegrationBuild=true`.
   - Builds the npm packages in:
     - `packages/identity-client-core/dist`
     - `packages/identity-angular-client/dist`
     - `packages/identity-react-client/dist`
     - `packages/identity-react-organizations/dist`.
   - Uploads each artefact set with versioned names for inspection.

No packages are published automatically during validation runs.

## Releasing

1. Ensure `main` contains the commits you want to release (and optionally push a `vX.Y.Z` tag).
2. From GitHub, trigger **Actions → CI → Run workflow** on the desired ref.
3. Optional inputs:
   - `package-version` – override the computed SemVer (normally omit if you tagged the commit).
   - `publish-to-nuget` – set to `true` to push `.nupkg`/`.snupkg` files to NuGet (requires `NUGET_API_KEY` secret).
   - `publish-to-npm` – set to `true` to publish the npm packages to npm (requires `NPM_TOKEN` secret; the workflow exports it as `NODE_AUTH_TOKEN`).
4. The workflow reuses the validation steps, aligns npm package versions via Node scripts, and pushes the NuGet packages plus all npm packages to the selected registries.

> Tip: use `nbgv tag` locally to stamp a `vX.Y.Z` tag before triggering the release so that subsequent builds continue with the next prerelease number.

## Checklist before shipping

- [ ] CI build on `main` is green.
- [ ] Secrets `NUGET_API_KEY` and/or `NPM_TOKEN` exist for publishing.
- [ ] (Optional) Run `nbgv prepare-release` / `nbgv tag` to produce the final SemVer and commit tag.
- [ ] Trigger the `CI` workflow via `workflow_dispatch` with the required publish flags.
- [ ] Verify the packages on NuGet/npm and update the changelog.
