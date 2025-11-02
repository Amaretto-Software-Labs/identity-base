# Org Sample Aspire Host

This Aspire app host runs the entire sample landscape:

- `org-sample-api` – the PostgreSQL-backed Identity/Base organisations sample.
- `org-sample-client` – React SPA demonstrating multi-organisation flows.
- `sample-api` & `sample-client` – the lightweight API/SPA pair that ships with the repository.
- `identity-postgres` – shared PostgreSQL instance (single database `identity_org_sample`).

## Prerequisites
- .NET 9 SDK installed.
- Node.js ≥ 18 available on your PATH.
- Run `npm install` once inside `apps/org-sample-client` and `apps/sample-client` before starting the host so the dev servers have their dependencies.
- A PostgreSQL instance that matches the connection string expected by the samples (defaults to `Host=localhost;Port=5432;Database=identity_org_sample;Username=postgres;Password=postgres`). Edit `ConnectionStrings:Primary` in `Samples.AppHost` settings or set the environment variable before launching if you use a different server/container.

## Running
```bash
dotnet run --project apps/Samples.AppHost/Samples.AppHost.csproj
```

The Aspire dashboard will open automatically. From there you can launch each web app:

| Resource | Default URL |
| --- | --- |
| org-sample-client | http://localhost:5173 |
| org-sample-api | http://localhost:8080 |
| sample-client | http://localhost:5174 |
| sample-api | http://localhost:8082 |
| Aspire dashboard | Usually http://localhost:18888 (shown in console output) |

> Identity.Base migrations are not applied automatically. If the shared database is new, run the Identity Base migrations once before starting the host, or allow the org sample API to apply them manually.

To stop everything, terminate the `dotnet run` process; Aspire tears down the containers for you.
