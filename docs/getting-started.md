# Getting Started

This guide walks through configuring and running Identity Base in a local environment.

## Prerequisites
- .NET SDK 9.0+
- PostgreSQL 16 (local or containerised)
- Optional: Docker Desktop for running the provided Postgres compose file

## Setup Steps
1. Clone the repository and restore dependencies:
   ```bash
   dotnet restore Identity.sln
   ```
2. Configure the database connection string in `Identity.Base/appsettings.Development.json` or via environment variables.
3. Adjust registration metadata in the `Registration` section. Each `ProfileField` entry defines:
   - `Name`: Key used in registration payload metadata
   - `DisplayName`: Human readable label
   - `Required`: Whether the field must be supplied
   - `MaxLength`: Maximum character length
   - `Pattern`: Optional regular expression for server-side validation
4. Replace the MailJet placeholders (`MailJet:ApiKey`, `MailJet:ApiSecret`, `MailJet:FromEmail`, `MailJet:Templates:Confirmation`) with valid values and, if you want operational alerts, enable `MailJet:ErrorReporting` with a monitored inbox. The service will fail to start without these credentials.
5. (Optional) Enable the seed administrator account by setting `IdentitySeed:Enabled` to `true` and providing `Email`, `Password`, and `Roles`.
6. Apply database migrations:
   ```bash
   dotnet ef database update --project Identity.Base/Identity.Base.csproj
   ```
7. Run the service:
   ```bash
   dotnet run --project Identity.Base/Identity.Base.csproj
   ```
8. Submit a registration request with metadata:
   ```bash
   curl -X POST https://localhost:5001/auth/register \
     -H "Content-Type: application/json" \
     -d '{
       "email": "user@example.com",
       "password": "Passw0rd!Passw0rd!",
       "metadata": {
         "displayName": "Example User",
         "company": "Example Co"
       }
     }'
   ```

## Email Templates
- MailJet integration is disabled by default. Populate `MailJet` API credentials, sender details, and `Templates.Confirmation` before setting `MailJet:Enabled` to `true`.
- When enabled, `/auth/register` sends the confirmation template with the following variables:
  - `email`
  - `displayName`
  - `confirmationUrl`

## Running Tests
- Integration tests run against the EF Core in-memory provider. Execute `dotnet test Identity.sln` before opening a pull request.
