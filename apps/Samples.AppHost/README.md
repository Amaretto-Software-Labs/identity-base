# Org Sample Aspire Host

This Aspire app host runs the entire sample landscape:

- `identity-base-host` – the Identity.Base sample host (local Identity Provider).
- `org-sample-api` – the PostgreSQL-backed Identity/Base organizations sample.
- `org-sample-client` – React SPA demonstrating multi-organization flows.
- `sample-api` & `sample-client` – the lightweight API/SPA pair that ships with the repository.

## Prerequisites
- .NET 9 SDK installed.
- Node.js ≥ 18 available on your PATH.
- Run `npm install` once inside `apps/org-sample-client` and `apps/sample-client` before starting the host so the dev servers have their dependencies.
- A PostgreSQL instance that matches the connection string expected by the samples (defaults to `Host=localhost;Port=5432;Database=identity_org_sample;Username=postgres;Password=P@ssword123`). Edit `ConnectionStrings:Primary` in `Samples.AppHost` settings or set the environment variable before launching if you use a different server/container.

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
| identity-base-host | http://localhost:8081 |
| Aspire dashboard | Usually http://localhost:18888 (shown in console output) |

> `org-sample-api` and `identity-base-host` apply EF Core migrations automatically on startup.

To stop everything, terminate the `dotnet run` process; Aspire tears down the containers for you.
