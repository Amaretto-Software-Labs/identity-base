# Identity Base

## Overview
Identity Base is a .NET 9 minimal API that centralises authentication, user management, and integration surfaces for the broader identity platform. It follows the Engineering Principles defined in `docs/reference/Engineering_Principles.md`, using extension modules to isolate concerns and feature folders for vertical slices (Authentication, Users, Email).

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

## Identity & Registration
- ASP.NET Core Identity is configured with GUID keys, strict password policy (12+ characters, upper/lower/digit), and enforced email confirmation.
- `Registration` options drive dynamic profile metadata. Configure fields under `Registration:ProfileFields` and ensure both confirmation and password reset URL templates include `{token}` and `{userId}` placeholders.
- `/auth/register` accepts payloads `{ email, password, metadata }`; metadata keys must match configured fields. Successful requests return `202 Accepted` with a correlation identifier and trigger a confirmation email.
- `IdentitySeed` options allow optional super-admin creation. Set `IdentitySeed:Enabled` to `true` and provide credentials (disabled by default).
- Mailjet delivery requires valid API credentials and template id; replace the placeholders in configuration before running the service and, if desired, enable `MailJet:ErrorReporting` to route failures to an operational inbox. Install the `Identity.Base.Email.MailJet` package and call `UseMailJetEmailSender()` to opt in.
- CORS is centrally configured via the `Cors` section; update `AllowedOrigins` to include every trusted frontend origin.

## Database & Migrations
- The `AppDbContext` extends `IdentityDbContext` and stores user metadata as JSONB (`Identity_Users.ProfileMetadata`). Tables use PascalCase with `Identity_` prefixes.
- Apply migrations locally: `dotnet ef database update --project Identity.Base/Identity.Base.csproj`.
- Add new schema changes with `dotnet ef migrations add <Name> --project Identity.Base/Identity.Base.csproj --output-dir Data/Migrations`.
- Test projects set `ConnectionStrings:Primary` to `InMemory:<Name>` to run against EF Core InMemory provider.

## Configuration Reference
```json
{
  "ConnectionStrings": {
    "Primary": "Host=localhost;Port=5432;Database=identity;Username=identity;Password=identity"
  },
  "Registration": {
    "ConfirmationUrlTemplate": "https://localhost:5001/account/confirm?token={token}&userId={userId}",
    "PasswordResetUrlTemplate": "https://localhost:5001/reset-password?token={token}&userId={userId}",
    "ProfileFields": [
      { "Name": "displayName", "DisplayName": "Display Name", "Required": true, "MaxLength": 128 },
      { "Name": "company", "DisplayName": "Company", "Required": false, "MaxLength": 128 }
    ]
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:3000", "https://localhost:5001"]
  },
  "MailJet": {
    "Enabled": true,
    "FromEmail": "noreply@example.com",
    "FromName": "Identity Base",
    "ApiKey": "YOUR_MAILJET_API_KEY",
    "ApiSecret": "YOUR_MAILJET_API_SECRET",
    "Templates": { "Confirmation": 1000000 },
    "ErrorReporting": {
      "Enabled": true,
      "Email": "identity-alerts@example.com"
    }
  }
}
```

## Next Steps
- Flesh out feature folders with real endpoints and contracts per sprint scope.
- Introduce database migrations and entities inside `Data/Configurations`.
- Model tables using PascalCase naming (apply the `Identity_` prefix only when necessary for clarity).
- Expand integration tests (future `Identity.Base.Tests`) alongside new functionality.
- Keep this documentation in sync with sprint deliverables and architecture decisions.
