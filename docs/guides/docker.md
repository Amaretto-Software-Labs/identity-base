# Docker & Compose Guide

This guide explains how to build and run Identity Base using Docker. The repository ships with a production-style `Dockerfile` and a local `docker-compose.local.yml` stack that includes PostgreSQL and an optional MailHog instance for catching emails during development.

## Prerequisites
- Docker Desktop 4.0+ or compatible engine
- Optional: a Mailjet account if you want to exercise real email delivery (otherwise configure MailHog or stub values)

## 1. Configure Environment Variables

Copy `.env.example` to `.env` and replace placeholders with values:

```bash
cp .env.example .env
```

Required settings:
- `CONNECTIONSTRINGS__PRIMARY` – points to the Postgres instance (`Host=postgres;Port=5432;Database=identity;Username=identity;Password=identity` works for the compose stack).
- `MAILJET__*` – Mailjet API credentials and template IDs. Supply real values for production or leave the defaults to satisfy option validation (email sends will fail if the keys are invalid).
- `MFA__ISSUER` – label displayed in authenticator apps.
- `EXTERNALPROVIDERS__*` – toggle and configure social login providers when available.

## 2. Build the Container Image

The multi-stage Dockerfile publishes the API from a .NET 9 SDK image and runs it as a non-root user inside the ASP.NET runtime image.

```bash
docker build -t identity-base:latest .
```

Key characteristics:
- Restores dependencies and publishes the API in a dedicated build stage (NuGet caching friendly).
- Runtime image exposes port `8080` and runs under a dedicated non-root user (`app`).
- `ASPNETCORE_URLS` defaults to `http://+:8080`; adjust or bind TLS in the hosting platform as needed.

## 3. Run the Local Stack with Compose

Use the provided compose file to spin up the API, PostgreSQL, and MailHog (for capturing outgoing email):

```bash
docker compose -f docker-compose.local.yml --env-file .env up --build
```

Services:
- `identity-api` – the Identity Base container listening on `http://localhost:8080`.
- `postgres` – PostgreSQL 16 with data persisted in the `postgres-data` volume.
- `mailhog` – accessible at `http://localhost:8025` for inspecting email traffic (enable the Mailjet package and set `MailJet:Enabled=true` if you want the host to send real messages).

Stop the stack with:

```bash
docker compose -f docker-compose.local.yml down
```

To wipe Postgres data, add `--volumes`.

## 4. Verify Health & Connectivity

Once the containers are running, confirm the health endpoint returns `Healthy`:

```bash
curl http://localhost:8080/healthz | jq
```

You should see checks for `database`, `mailjet`, and `externalProviders`.

## 5. Troubleshooting

| Issue | Resolution |
| --- | --- |
| API exits immediately with Mailjet validation errors | Ensure `.env` contains non-empty `MAILJET__*` values. For local testing you can set any numeric template IDs and placeholder keys; email sends will fail but the service will start. |
| Cannot connect to Postgres | Confirm `CONNECTIONSTRINGS__PRIMARY` uses the `postgres` hostname and that the container is healthy (`docker compose ps`). |
| Need HTTPS locally | Terminate TLS with a reverse proxy (e.g., Traefik, Caddy) and forward plain HTTP to the container’s `8080` port, or override `ASPNETCORE_URLS` to use `https://` with mounted certificates. |
| Social login callbacks fail | Set the respective provider environment variables and ensure the redirect URIs registered with the provider match your compose hostname (e.g., `http://localhost:8080`). |

## 6. Next Steps

- Integrate the Docker build into CI (`dotnet test` + `docker build`) to catch regressions before merging.
- Publish the image to a registry (e.g., GitHub Container Registry) using `docker buildx` or your platform’s pipelines.
- Extend the compose stack with additional infrastructure (Redis, monitoring) as needed for your deployment target.
