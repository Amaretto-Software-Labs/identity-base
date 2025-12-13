---
id: playbooks/admin-api-smoke
title: Admin API Smoke (Users, Roles, Permissions)
version: 0.1.0
last_reviewed: 2025-11-05
tags: [admin, roles, users]
required_roles: [Developer]
prerequisites:
  dotnet: "9.x"
  database: "PostgreSQL 16"
  repo_root: "cloned"
required_secrets:
  - CONNECTIONSTRINGS__PRIMARY
---

# Goal
Exercise key Admin endpoints end-to-end: list permissions, create a user, assign roles, lock/unlock, and soft delete/restore. Verify success criteria and expected responses.

# Preconditions
- Identity Host running and exposing `/admin/*` endpoints.
- Admin scope enabled (`IdentityAdmin:RequiredScope=identity.admin`) and granted to the SPA/client you use for tokens.
- Seeded admin user exists with the required permissions (`users.read`, `users.create`, `users.update`, `users.lock`, `users.manage-roles`, `users.delete`, `roles.read`, `roles.manage`).

# Resources
- Admin package: docs/packages/identity-base-admin/index.md
- Roles package: docs/packages/identity-base-roles/index.md

# Command Steps
Command: Obtain admin token (authorization code + PKCE)
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

ADMIN_TOKEN=$(get_access_token "admin@example.com" "P@ssword12345!" "openid profile email offline_access identity.api identity.admin"); test -n "$ADMIN_TOKEN" && echo OK || echo FAIL
```

Command: List canonical permissions
```bash
curl -s "http://localhost:8080/admin/permissions?page=1&pageSize=50" -H "Authorization: Bearer $ADMIN_TOKEN" | jq '{count: (.items | length)}'
```

Command: Create a user (returns 201 Created)
```bash
NEW_EMAIL="test.user+$(date +%s)@example.com"; CREATE_RESP=$(curl -s -X POST http://localhost:8080/admin/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{ \"email\": \"$NEW_EMAIL\", \"password\": \"P@ssword12345!\", \"emailConfirmed\": true }" ); echo "$CREATE_RESP" | jq '.'; NEW_USER_ID=$(echo "$CREATE_RESP" | jq -r '.id // empty'); echo $NEW_USER_ID
```

Command: Fetch created user details
```bash
curl -s http://localhost:8080/admin/users/$NEW_USER_ID -H "Authorization: Bearer $ADMIN_TOKEN" | jq '{id, email, emailConfirmed, isLockedOut}'
```

Command: Assign StandardUser role
```bash
curl -s -X PUT http://localhost:8080/admin/users/$NEW_USER_ID/roles \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '["StandardUser"]' -i | head -n1
```

Command: Verify roles for the user
```bash
curl -s http://localhost:8080/admin/users/$NEW_USER_ID/roles -H "Authorization: Bearer $ADMIN_TOKEN" | jq '.'
```

Command: Lock the user (30-day default)
```bash
curl -s -X POST http://localhost:8080/admin/users/$NEW_USER_ID/lock \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"minutes": 5}' -i | head -n1
```

Command: Unlock the user
```bash
curl -s -X POST http://localhost:8080/admin/users/$NEW_USER_ID/unlock -H "Authorization: Bearer $ADMIN_TOKEN" -i | head -n1
```

Optional Step 8: Force password reset (202 Accepted)
Command: curl -s -X POST http://localhost:8080/admin/users/$NEW_USER_ID/force-password-reset -H "Authorization: Bearer $ADMIN_TOKEN" -i | head -n1
```bash
curl -s -X POST http://localhost:8080/admin/users/$NEW_USER_ID/force-password-reset -H "Authorization: Bearer $ADMIN_TOKEN" -i | head -n1
```

Command: Soft delete the user
```bash
curl -s -X DELETE http://localhost:8080/admin/users/$NEW_USER_ID -H "Authorization: Bearer $ADMIN_TOKEN" -i | head -n1
```

Command: Restore the user
```bash
curl -s -X POST http://localhost:8080/admin/users/$NEW_USER_ID/restore -H "Authorization: Bearer $ADMIN_TOKEN" -i | head -n1
```

# Verification
- Permissions endpoint returns a non-zero item count.
- User creation returns 201 and a valid `id`.
- Roles endpoint reflects `StandardUser` after assignment.
- Lock/unlock endpoints return 204 No Content.
- Optional password reset returns 202 Accepted.
- Delete returns 204 No Content, restore returns 204 No Content.

# Diagram
```mermaid
sequenceDiagram
  participant Admin as Admin Client
  participant Host as Identity Host
  Admin->>Host: POST /connect/token (identity.admin)
  Host-->>Admin: 200 OK { access_token }
  Admin->>Host: GET /admin/permissions
  Admin->>Host: POST /admin/users
  Host-->>Admin: 201 Created { id }
  Admin->>Host: PUT /admin/users/{id}/roles [StandardUser]
  Admin->>Host: POST /admin/users/{id}/lock / unlock
  Admin->>Host: DELETE /admin/users/{id} / POST restore
```

# Outputs
- `NEW_USER_ID` (created user id) and evidence of role assignment/lock state changes.

# Completion Checklist
- [ ] Admin token acquired.
- [ ] Permissions list non-empty.
- [ ] User created and retrievable.
- [ ] Role assignment reflected.
- [ ] Lock/unlock and delete/restore endpoints succeed.
