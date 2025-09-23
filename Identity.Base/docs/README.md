# Identity Base

## Overview
Identity Base is a .NET 9 minimal API that centralises authentication, user management, and integration surfaces for the broader identity platform. It follows the Engineering Principles defined in `docs/Engineering_Principles.md`, using extension modules to isolate concerns and feature folders for vertical slices (Authentication, Users, Email).

## Prerequisites
- .NET SDK 9.0+
- PostgreSQL 16 (local or containerised) reachable with the connection string in `appsettings.Development.json`
- Optional: Docker for running the provided PostgreSQL compose snippet

## Getting Started
1. Restore packages and build the solution: `dotnet restore` then `dotnet build Identity.sln`.
2. Ensure PostgreSQL is running with the credentials from the repository README.
3. Run the API from the repository root: `dotnet run --project Identity.Base/Identity.Base.csproj`.
4. Browse to `/healthz` to confirm application and database connectivity; `/openapi/v1.json` is exposed in development.

## Project Structure
- `Program.cs` bootstraps the host and delegates registrations to extension modules.
- `Extensions/` contains DI, middleware, and endpoint mapping helpers.
- `Features/` holds feature folders (Authentication, Users, Email) with endpoint groups.
- `Data/` provides the `AppDbContext` for EF Core and future entity configurations.
- `Options/` contains configuration bindings such as `DatabaseOptions`.

## Next Steps
- Flesh out feature folders with real endpoints and contracts per sprint scope.
- Introduce database migrations and entities inside `Data/Configurations`.
- Model tables using PascalCase naming (apply the `Identity_` prefix only when necessary for clarity).
- Expand integration tests (future `Identity.Base.Tests`) alongside new functionality.
- Keep this documentation in sync with sprint deliverables and architecture decisions.
