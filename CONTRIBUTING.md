# Contributing

Thanks for your interest in contributing to Identity Base! We welcome bug reports, feature ideas, documentation improvements, and pull requests.

## Before You Start
- Review the [Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by it.
- Skim the [Engineering Principles](docs/Engineering_Principles.md) and [Database Design Guidelines](docs/Database_Design_Guidelines.md) to understand the architectural approach.
- Check existing [issues](https://github.com/amaretto-labs/identity-base/issues) to avoid duplicates and to find work that is ready for help.

## How to Contribute

### Reporting Issues
1. Search open issues to see if your problem has already been reported.
2. If not, open a new issue and include:
   - A clear description of the bug or feature request
   - Steps to reproduce (for bugs)
   - Expected vs. actual behaviour
   - Environment details (OS, .NET SDK version, database, etc.)

### Submitting Changes
1. Fork the repository and create a topic branch: `git checkout -b feature/short-description`.
2. Make your changes, keeping commits focused and following existing code style.
3. Update or add tests (run `dotnet test Identity.sln`).
4. Update relevant docs (README, getting-started guides, release notes) when behaviour changes.
5. Run `dotnet build Identity.sln` and `dotnet test Identity.sln` before opening a PR.
6. Open a pull request against `main` with:
   - A concise summary of the change
   - A link to the related issue (if any)
   - Testing notes

### Code Review Expectations
- At least one maintainer review is required before merge.
- Please respond to feedback promptly. If you cannot address a comment in the current PR, note it and open a follow-up issue.
- Keep discussions respectful and aligned with the Code of Conduct.

## Development Tips
- The solution targets .NET 9.0. Install the matching SDK.
- The reference host is `Identity.Base.Host`. Use `dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj` to exercise the full stack locally.
- Use the provided GitHub Actions workflow (`CI`) to pack preview NuGet packages via the manual **Run workflow** trigger when you want to verify distribution artefacts.

## License
By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
