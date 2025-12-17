---
id: playbooks/full-stack-smoke-test
title: Full-Stack Smoke Test (Host + Health + Token + Orgs)
version: 0.1.0
last_reviewed: 2025-11-05
tags: [smoke, full-stack, verification]
required_roles: [Developer]
prerequisites:
  dotnet: "9.x"
  database: "PostgreSQL 16"
  repo_root: "cloned"
  env_files: [".env"]
required_secrets:
  - CONNECTIONSTRINGS__PRIMARY
---

# Goal
Run the Identity Host end-to-end, validate health checks, obtain a token, and exercise protected endpoints. Optionally verify Organizations endpoints if wired.

# Preconditions
- Seed admin user configured (see Roles/Org seed playbook) or `IdentitySeed.Enabled=true` with credentials.
- If testing via Docker Compose, `.env` contains required variables.

# Resources
- Getting Started: docs/guides/getting-started.md
- Full-Stack Integration Guide: docs/guides/full-stack-integration-guide.md
- Health and endpoints: docs/packages/identity-base/index.md

# Command Steps
Optional Step 1: Start Postgres and Mailhog
Command: docker compose -f docker-compose.local.yml up -d postgres mailhog
```bash
docker compose -f docker-compose.local.yml up -d postgres mailhog
```

Command: Build and run tests
```bash
dotnet build -c Debug Identity.sln && dotnet test -c Debug Identity.sln --nologo
```

Command: Start the Identity Host (without Docker)
```bash
ConnectionStrings__Primary="Host=localhost;Database=identity;Username=identity;Password=identity" dotnet run --project Identity.Base.Host
```

# Verification
Command: Health check overall status
```bash
curl -s http://localhost:8080/healthz | jq -r '.status'
```
Expect: Healthy

Command: Health check details (database, externalProviders, mailjet if enabled)
```bash
curl -s http://localhost:8080/healthz | jq '[.checks[] | {name, status}]'
```
Expect: Includes `{ name: "database", status: "Healthy" }`. If Mailjet enabled, `{ name: "mailjet", status: "Healthy" }`.

Command: Obtain access token via authorization code + PKCE (seed admin)
```bash
get_access_token() {
  local email="$1"
  local password="$2"
  local scope="$3"
  local base_url="${BASE_URL:-http://localhost:8080}"
  local redirect_uri="${REDIRECT_URI:-http://localhost:5173/auth/callback}"

  local cookie_jar verifier challenge state location code token
  cookie_jar=$(mktemp)

  verifier=$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=' | tr -d '\n')
  challenge=$(printf '%s' "$verifier" | openssl dgst -binary -sha256 | openssl base64 -A | tr '+/' '-_' | tr -d '=')
  state=$(openssl rand -hex 16)

  jq -n --arg email "$email" --arg password "$password" --arg clientId "spa-client" \
    '{email:$email,password:$password,clientId:$clientId}' \
    | curl -fsS -c "$cookie_jar" -X POST "$base_url/auth/login" -H "Content-Type: application/json" -d @- >/dev/null

  location=$(curl -fsS -i -o /dev/null -b "$cookie_jar" -G "$base_url/connect/authorize" \
    --data-urlencode "response_type=code" \
    --data-urlencode "client_id=spa-client" \
    --data-urlencode "redirect_uri=$redirect_uri" \
    --data-urlencode "scope=$scope" \
    --data-urlencode "code_challenge=$challenge" \
    --data-urlencode "code_challenge_method=S256" \
    --data-urlencode "state=$state" \
    | awk 'BEGIN{IGNORECASE=1} /^location:/{print $2}' | tr -d '\r')

  code=$(printf '%s' "$location" | sed -n 's/.*[?&]code=\\([^&]*\\).*/\\1/p')

  token=$(curl -fsS -X POST "$base_url/connect/token" -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=authorization_code" \
    --data-urlencode "code=$code" \
    --data-urlencode "redirect_uri=$redirect_uri" \
    --data-urlencode "client_id=spa-client" \
    --data-urlencode "code_verifier=$verifier" \
    | jq -r .access_token)

  rm -f "$cookie_jar"
  printf '%s' "$token"
}

ACCESS_TOKEN=$(get_access_token "admin@example.com" "P@ssword12345!" "openid profile email offline_access identity.api identity.admin"); test -n "$ACCESS_TOKEN" && echo OK || echo FAIL
```
Expect: OK (non-empty access token captured in ACCESS_TOKEN)

Optional Step 4: Verify roles/permissions endpoint
Command: curl -s http://localhost:8080/users/me/permissions -H "Authorization: Bearer $ACCESS_TOKEN" | jq '.permissions | length'
```bash
curl -s http://localhost:8080/users/me/permissions -H "Authorization: Bearer $ACCESS_TOKEN" | jq '.permissions | length'
```
Expect: A non-zero count when admin roles are seeded.

Optional Step 5: Verify organizations endpoints (if mapped)
Command: curl -s http://localhost:8080/organizations -H "Authorization: Bearer $ACCESS_TOKEN" | jq 'length'
```bash
curl -s http://localhost:8080/organizations -H "Authorization: Bearer $ACCESS_TOKEN" | jq 'length'
```
Expect: Zero or more organizations; non-error response confirms mapping and auth.

# Diagram
```mermaid
sequenceDiagram
  participant CLI as Shell
  participant Host as Identity Host
  participant DB as PostgreSQL
  CLI->>Host: GET /healthz
  Host->>DB: DB check
  Host-->>CLI: { status: Healthy }
  CLI->>Host: Authorization code + PKCE
  Host->>DB: Validate user
  Host-->>CLI: 200 OK + access_token
  CLI->>Host: GET /users/me/permissions (Bearer)
  Host-->>CLI: { permissions: [...] }
```

# Outputs
- Confirmation that the host is healthy and issuing tokens.
- Optional validation of permissions and organization endpoints.

# Completion Checklist
- [ ] `dotnet build` and `dotnet test` succeeded.
- [ ] `/healthz` status is `Healthy`.
- [ ] Token obtained successfully for the seed admin.
- [ ] Optional: permissions listed; organizations endpoint reachable.
