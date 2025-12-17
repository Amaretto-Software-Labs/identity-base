# OpenAPI Endpoint Reference (generated)

Generated from the Development OpenAPI document served by `apps/org-sample-api` at `/openapi/v1.json` (2025-12-17T15:40:33.825Z).

- OpenAPI title: `OrgSampleApi | v1`
- OpenAPI version: `1.0.0`
- Paths: `63`, operations: `87`

> Note: OpenIddict protocol endpoints like `/connect/authorize` and `/connect/token` are not described in this OpenAPI document.

## Contents

- [Admin.Permissions](#admin-permissions)
- [Admin.Roles](#admin-roles)
- [Admin.Users](#admin-users)
- [Authentication](#authentication)
- [AuthorizeEndpoint](#authorizeendpoint)
- [OrgSampleApi](#orgsampleapi)
- [Sample](#sample)
- [Users](#users)

## Admin.Permissions

- `GET /admin/permissions` — Returns a paged list of permissions and usage counts.

### GET /admin/permissions

- OperationId: `AdminListPermissions`
- Summary: Returns a paged list of permissions and usage counts.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| Sort | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `PagedResultOfAdminPermissionSummary` |
| 403 | `application/problem+json` | `ProblemDetails` |

## Admin.Roles

- `GET /admin/roles` — Returns a paged list of roles and their permissions.
- `POST /admin/roles` — Creates a new role definition with permissions.
- `PUT /admin/roles/{id}` — Updates role metadata and permissions.
- `DELETE /admin/roles/{id}` — Deletes a role when not system and unused.

### GET /admin/roles

- OperationId: `AdminListRoles`
- Summary: Returns a paged list of roles and their permissions.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| IsSystemRole | query | `boolean` | no |  |
| Sort | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `PagedResultOfAdminRoleSummary` |
| 403 | `application/problem+json` | `ProblemDetails` |

### POST /admin/roles

- OperationId: `AdminCreateRole`
- Summary: Creates a new role definition with permissions.
- Auth: includes `403` responses
**Request Body**

- Content types: `application/json`
- Schema: `AdminRoleCreateRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 201 | `application/json` | `AdminRoleDetail` |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |

### PUT /admin/roles/{id}

- OperationId: `AdminUpdateRole`
- Summary: Updates role metadata and permissions.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AdminRoleUpdateRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `AdminRoleDetail` |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |
| 409 | `application/problem+json` | `ProblemDetails` |

### DELETE /admin/roles/{id}

- OperationId: `AdminDeleteRole`
- Summary: Deletes a role when not system and unused.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |
| 409 | `application/problem+json` | `ProblemDetails` |

## Admin.Users

- `GET /admin/users` — Returns a paged list of users for administration.
- `POST /admin/users` — Creates a new user with optional roles and invitation emails.
- `GET /admin/users/{id}` — Returns details for a specific user, including roles and MFA state.
- `PUT /admin/users/{id}` — Updates user profile flags and metadata.
- `DELETE /admin/users/{id}` — Soft deletes the user by disabling access.
- `POST /admin/users/{id}/force-password-reset` — Generates a password reset token and sends invitation email to the user.
- `POST /admin/users/{id}/lock` — Locks a user account until explicitly unlocked.
- `POST /admin/users/{id}/mfa/reset` — Disables MFA for the user and resets the authenticator key.
- `POST /admin/users/{id}/resend-confirmation` — Resends the account confirmation email if the user is unconfirmed.
- `POST /admin/users/{id}/restore` — Restores a previously soft-deleted user.
- `GET /admin/users/{id}/roles` — Returns the set of roles currently assigned to the user.
- `PUT /admin/users/{id}/roles` — Replaces the role assignments for the user.
- `POST /admin/users/{id}/unlock` — Clears the lockout state for a user account.

### GET /admin/users

- OperationId: `AdminListUsers`
- Summary: Returns a paged list of users for administration.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| Sort | query | `string` | no |  |
| Role | query | `string` | no |  |
| Locked | query | `boolean` | no |  |
| Deleted | query | `boolean` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `PagedResultOfAdminUserSummary` |
| 403 | `application/problem+json` | `ProblemDetails` |

### POST /admin/users

- OperationId: `AdminCreateUser`
- Summary: Creates a new user with optional roles and invitation emails.
- Auth: includes `403` responses
**Request Body**

- Content types: `application/json`
- Schema: `AdminUserCreateRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 201 |  |  |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |

### GET /admin/users/{id}

- OperationId: `AdminGetUser`
- Summary: Returns details for a specific user, including roles and MFA state.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `AdminUserDetailResponse` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### PUT /admin/users/{id}

- OperationId: `AdminUpdateUser`
- Summary: Updates user profile flags and metadata.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AdminUserUpdateRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `AdminUserDetailResponse` |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |
| 409 | `application/problem+json` | `ProblemDetails` |

### DELETE /admin/users/{id}

- OperationId: `AdminSoftDeleteUser`
- Summary: Soft deletes the user by disabling access.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/force-password-reset

- OperationId: `AdminForcePasswordReset`
- Summary: Generates a password reset token and sends invitation email to the user.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/lock

- OperationId: `AdminLockUser`
- Summary: Locks a user account until explicitly unlocked.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AdminUserLockRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/mfa/reset

- OperationId: `AdminResetMfa`
- Summary: Disables MFA for the user and resets the authenticator key.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/resend-confirmation

- OperationId: `AdminResendConfirmation`
- Summary: Resends the account confirmation email if the user is unconfirmed.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/restore

- OperationId: `AdminRestoreUser`
- Summary: Restores a previously soft-deleted user.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### GET /admin/users/{id}/roles

- OperationId: `AdminGetUserRoles`
- Summary: Returns the set of roles currently assigned to the user.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 | `application/json` | `AdminUserRolesResponse` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### PUT /admin/users/{id}/roles

- OperationId: `AdminUpdateUserRoles`
- Summary: Replaces the role assignments for the user.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AdminUserRolesUpdateRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

### POST /admin/users/{id}/unlock

- OperationId: `AdminUnlockUser`
- Summary: Clears the lockout state for a user account.
- Auth: includes `403` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| id | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 403 | `application/problem+json` | `ProblemDetails` |
| 404 |  |  |

## Authentication

- `POST /auth/confirm-email` — Confirms a user's email address using the confirmation token.
- `DELETE /auth/external/{provider}` — Removes a linked external authentication provider from the current user.
- `GET /auth/external/{provider}/callback` — Handles the callback from an external authentication provider.
- `GET /auth/external/{provider}/start` — Starts an external authentication challenge for the specified provider.
- `POST /auth/forgot-password` — Generates a password reset token and sends the reset email if the account exists.
- `POST /auth/login` — Authenticates a user and establishes an Identity cookie for subsequent authorization flows.
- `POST /auth/logout` — Signs the current user out of the Identity cookie session.
- `POST /auth/mfa/challenge` — Sends an MFA challenge via the selected method (e.g., SMS).
- `POST /auth/mfa/disable` — Disables authenticator MFA for the current user.
- `POST /auth/mfa/enroll` — Starts authenticator app enrollment and returns the shared key and otpauth URI.
- `POST /auth/mfa/recovery-codes` — Generates new recovery codes for the current user.
- `POST /auth/mfa/verify` — Verifies an authenticator code. Works for both enrollment and login step-up.
- `GET /auth/profile-schema` — Returns the configured profile field definitions.
- `POST /auth/register` — Registers a new user with metadata and triggers confirmation email.
- `POST /auth/resend-confirmation` — Resends the confirmation email when the account is not yet confirmed.
- `POST /auth/reset-password` — Resets the user's password using the provided token.

### POST /auth/confirm-email

- OperationId: `ConfirmEmail`
- Summary: Confirms a user's email address using the confirmation token.
**Request Body**

- Content types: `application/json`
- Schema: `ConfirmEmailRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### DELETE /auth/external/{provider}

- OperationId: `UnlinkExternalAuthentication`
- Summary: Removes a linked external authentication provider from the current user.
- Auth: includes `401` responses

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| provider | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |
| 401 | `application/problem+json` | `ProblemDetails` |

### GET /auth/external/{provider}/callback

- OperationId: `CompleteExternalAuthentication`
- Summary: Handles the callback from an external authentication provider.

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| provider | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 302 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### GET /auth/external/{provider}/start

- OperationId: `StartExternalAuthentication`
- Summary: Starts an external authentication challenge for the specified provider.

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| provider | path | `string` | yes |  |
| ReturnUrl | query | `string` | no |  |
| Mode | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 302 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### POST /auth/forgot-password

- OperationId: `ForgotPassword`
- Summary: Generates a password reset token and sends the reset email if the account exists.
**Request Body**

- Content types: `application/json`
- Schema: `ForgotPasswordRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |

### POST /auth/login

- OperationId: `Login`
- Summary: Authenticates a user and establishes an Identity cookie for subsequent authorization flows.

Notes:
- Browser-based requests may receive `403` if the `Origin` header is present and not allowed by `Cors:AllowedOrigins` (CSRF protection for cookie-based endpoints).
**Request Body**

- Content types: `application/json`
- Schema: `LoginRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### POST /auth/logout

- OperationId: `Logout`
- Summary: Signs the current user out of the Identity cookie session.

Notes:
- Browser-based requests may receive `403` if the `Origin` header is present and not allowed by `Cors:AllowedOrigins` (CSRF protection for cookie-based endpoints).
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /auth/mfa/challenge

- OperationId: `SendMfaChallenge`
- Summary: Sends an MFA challenge via the selected method (e.g., SMS).
**Request Body**

- Content types: `application/json`
- Schema: `MfaChallengeRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### POST /auth/mfa/disable

- OperationId: `DisableMfa`
- Summary: Disables authenticator MFA for the current user.
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### POST /auth/mfa/enroll

- OperationId: `EnrollMfa`
- Summary: Starts authenticator app enrollment and returns the shared key and otpauth URI.
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /auth/mfa/recovery-codes

- OperationId: `RegenerateRecoveryCodes`
- Summary: Generates new recovery codes for the current user.
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### POST /auth/mfa/verify

- OperationId: `VerifyMfa`
- Summary: Verifies an authenticator code. Works for both enrollment and login step-up.
**Request Body**

- Content types: `application/json`
- Schema: `MfaVerifyRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

### GET /auth/profile-schema

- OperationId: `GetProfileSchema`
- Summary: Returns the configured profile field definitions.
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /auth/register

- OperationId: `RegisterUser`
- Summary: Registers a new user with metadata and triggers confirmation email.
**Request Body**

- Content types: `application/json`
- Schema: `RegisterUserRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |

### POST /auth/resend-confirmation

- OperationId: `ResendConfirmationEmail`
- Summary: Resends the confirmation email when the account is not yet confirmed.
**Request Body**

- Content types: `application/json`
- Schema: `ResendConfirmationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |

### POST /auth/reset-password

- OperationId: `ResetPassword`
- Summary: Resets the user's password using the provided token.
**Request Body**

- Content types: `application/json`
- Schema: `ResetPasswordRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |

## AuthorizeEndpoint

- `GET /connect/authorize`
- `POST /connect/authorize`

### GET /connect/authorize

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /connect/authorize

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

## OrgSampleApi

- `GET /admin/organizations`
- `POST /admin/organizations`
- `GET /admin/organizations/{organizationId}`
- `PATCH /admin/organizations/{organizationId}`
- `DELETE /admin/organizations/{organizationId}`
- `GET /admin/organizations/{organizationId}/invitations`
- `POST /admin/organizations/{organizationId}/invitations`
- `DELETE /admin/organizations/{organizationId}/invitations/{code}`
- `GET /admin/organizations/{organizationId}/members`
- `POST /admin/organizations/{organizationId}/members`
- `PUT /admin/organizations/{organizationId}/members/{userId}`
- `DELETE /admin/organizations/{organizationId}/members/{userId}`
- `GET /admin/organizations/{organizationId}/roles`
- `POST /admin/organizations/{organizationId}/roles`
- `DELETE /admin/organizations/{organizationId}/roles/{roleId}`
- `GET /admin/organizations/{organizationId}/roles/{roleId}/permissions`
- `PUT /admin/organizations/{organizationId}/roles/{roleId}/permissions`
- `GET /invitations/{code}`
- `POST /invitations/claim`
- `GET /users/me/organizations`
- `POST /users/me/organizations`
- `GET /users/me/organizations/{organizationId}`
- `PATCH /users/me/organizations/{organizationId}`
- `GET /users/me/organizations/{organizationId}/invitations`
- `POST /users/me/organizations/{organizationId}/invitations`
- `DELETE /users/me/organizations/{organizationId}/invitations/{code}`
- `GET /users/me/organizations/{organizationId}/members`
- `POST /users/me/organizations/{organizationId}/members`
- `PUT /users/me/organizations/{organizationId}/members/{userId}`
- `DELETE /users/me/organizations/{organizationId}/members/{userId}`
- `GET /users/me/organizations/{organizationId}/roles`
- `POST /users/me/organizations/{organizationId}/roles`
- `DELETE /users/me/organizations/{organizationId}/roles/{roleId}`
- `GET /users/me/organizations/{organizationId}/roles/{roleId}/permissions`
- `PUT /users/me/organizations/{organizationId}/roles/{roleId}/permissions`
- `GET /users/me/permissions`

### GET /admin/organizations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| TenantId | query | `string` | no |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| Status | query | `integer` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /admin/organizations

- OperationId: ``
**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /admin/organizations/{organizationId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PATCH /admin/organizations/{organizationId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateOrganizationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /admin/organizations/{organizationId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /admin/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /admin/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationInvitationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /admin/organizations/{organizationId}/invitations/{code}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| code | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /admin/organizations/{organizationId}/members

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| RoleId | query | `string` | no |  |
| Sort | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /admin/organizations/{organizationId}/members

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AddMembershipRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PUT /admin/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateMembershipRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /admin/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /admin/organizations/{organizationId}/roles

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| tenantId | query | `string` | no |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /admin/organizations/{organizationId}/roles

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationRoleRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /admin/organizations/{organizationId}/roles/{roleId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /admin/organizations/{organizationId}/roles/{roleId}/permissions

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PUT /admin/organizations/{organizationId}/roles/{roleId}/permissions

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateOrganizationRolePermissionsRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /invitations/{code}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| code | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /invitations/claim

- OperationId: ``
**Request Body**

- Content types: `application/json`
- Schema: `ClaimOrganizationInvitationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| tenantId | query | `string` | no |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| IncludeArchived | query | `boolean` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /users/me/organizations

- OperationId: ``
**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations/{organizationId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PATCH /users/me/organizations/{organizationId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateOrganizationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /users/me/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationInvitationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /users/me/organizations/{organizationId}/invitations/{code}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| code | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations/{organizationId}/members

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |
| RoleId | query | `string` | no |  |
| Sort | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /users/me/organizations/{organizationId}/members

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `AddMembershipRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PUT /users/me/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateMembershipRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /users/me/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations/{organizationId}/roles

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| tenantId | query | `string` | no |  |
| Page | query | `integer` | no |  |
| PageSize | query | `integer` | no |  |
| Search | query | `string` | no |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /users/me/organizations/{organizationId}/roles

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `CreateOrganizationRoleRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /users/me/organizations/{organizationId}/roles/{roleId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/organizations/{organizationId}/roles/{roleId}/permissions

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PUT /users/me/organizations/{organizationId}/roles/{roleId}/permissions

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| roleId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateOrganizationRolePermissionsRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /users/me/permissions

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

## Sample

- `GET /sample/defaults`
- `GET /sample/invitations/{code}`
- `POST /sample/invitations/claim`
- `POST /sample/invitations/register` — Registers a new user through an invitation and assigns organization membership.
- `GET /sample/organizations/{organizationId}/invitations`
- `POST /sample/organizations/{organizationId}/invitations`
- `DELETE /sample/organizations/{organizationId}/invitations/{code}`
- `GET /sample/organizations/{organizationId}/members`
- `PATCH /sample/organizations/{organizationId}/members/{userId}`
- `DELETE /sample/organizations/{organizationId}/members/{userId}`
- `GET /sample/registration/profile-fields`
- `GET /sample/status`

### GET /sample/defaults

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /sample/invitations/{code}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| code | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /sample/invitations/claim

- OperationId: ``
**Request Body**

- Content types: `application/json`
- Schema: `ClaimInvitationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /sample/invitations/register

- OperationId: `RegisterWithInvitation`
- Summary: Registers a new user through an invitation and assigns organization membership.
**Request Body**

- Content types: `application/json`
- Schema: `InvitationRegistrationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 202 |  |  |
| 400 | `application/problem+json` | `HttpValidationProblemDetails` |
| 404 |  |  |

### GET /sample/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### POST /sample/organizations/{organizationId}/invitations

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `CreateInvitationRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /sample/organizations/{organizationId}/invitations/{code}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| code | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /sample/organizations/{organizationId}/members

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### PATCH /sample/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Request Body**

- Content types: `application/json`
- Schema: `UpdateOrganizationMemberRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### DELETE /sample/organizations/{organizationId}/members/{userId}

- OperationId: ``

**Parameters**

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| organizationId | path | `string` | yes |  |
| userId | path | `string` | yes |  |

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /sample/registration/profile-fields

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

### GET /sample/status

- OperationId: ``
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |

## Users

- `GET /users/me` — Returns the current user's profile and metadata.
- `POST /users/me/change-password` — Changes the current user's password.
- `PUT /users/me/profile` — Updates the current user's profile metadata.

### GET /users/me

- OperationId: `GetCurrentUser`
- Summary: Returns the current user's profile and metadata.
- Auth: includes `401` responses
**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 401 | `application/problem+json` | `ProblemDetails` |

### POST /users/me/change-password

- OperationId: `ChangePassword`
- Summary: Changes the current user's password.
- Auth: includes `401` responses
**Request Body**

- Content types: `application/json`
- Schema: `ChangePasswordRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 204 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |
| 401 | `application/problem+json` | `ProblemDetails` |

### PUT /users/me/profile

- OperationId: `UpdateUserProfile`
- Summary: Updates the current user's profile metadata.
- Auth: includes `401` responses
**Request Body**

- Content types: `application/json`
- Schema: `UpdateProfileRequest`

**Responses**

| Status | Content types | Schema |
| --- | --- | --- |
| 200 |  |  |
| 400 | `application/problem+json` | `ProblemDetails` |
| 401 | `application/problem+json` | `ProblemDetails` |
| 409 | `application/problem+json` | `ProblemDetails` |

## Component Schemas

This OpenAPI document defines `42` schemas under `components.schemas`.

- `AddMembershipRequest`
- `AdminPermissionSummary`
- `AdminRoleCreateRequest`
- `AdminRoleDetail`
- `AdminRoleSummary`
- `AdminRoleUpdateRequest`
- `AdminUserCreateRequest`
- `AdminUserDetailResponse`
- `AdminUserExternalLogin`
- `AdminUserLockRequest`
- `AdminUserRolesResponse`
- `AdminUserRolesUpdateRequest`
- `AdminUserSummary`
- `AdminUserUpdateRequest`
- `ChangePasswordRequest`
- `ClaimInvitationRequest`
- `ClaimOrganizationInvitationRequest`
- `ConfirmEmailRequest`
- `CreateInvitationRequest`
- `CreateOrganizationInvitationRequest`
- `CreateOrganizationRequest`
- `CreateOrganizationRoleRequest`
- `ForgotPasswordRequest`
- `HttpValidationProblemDetails`
- `InvitationRegistrationRequest`
- `LoginRequest`
- `MfaChallengeRequest`
- `MfaVerifyRequest`
- `NullableOfOrganizationStatus`
- `OrganizationMetadata`
- `PagedResultOfAdminPermissionSummary`
- `PagedResultOfAdminRoleSummary`
- `PagedResultOfAdminUserSummary`
- `ProblemDetails`
- `RegisterUserRequest`
- `ResendConfirmationRequest`
- `ResetPasswordRequest`
- `UpdateMembershipRequest`
- `UpdateOrganizationMemberRequest`
- `UpdateOrganizationRequest`
- `UpdateOrganizationRolePermissionsRequest`
- `UpdateProfileRequest`
