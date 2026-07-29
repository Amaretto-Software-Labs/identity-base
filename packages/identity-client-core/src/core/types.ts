// Authentication Configuration
export interface IdentityConfig {
  // Required
  apiBase: string
  clientId: string

  // OAuth2 settings
  redirectUri: string
  scope?: string

  // Token settings
  tokenStorage?: 'localStorage' | 'sessionStorage' | 'memory'
  autoRefresh?: boolean

  // API settings
  timeout?: number
  retries?: number
}

// User Profile
export interface UserProfile {
  id: string
  email: string
  displayName: string
  emailConfirmed: boolean
  metadata: Record<string, string | null>
  concurrencyStamp: string
  twoFactorEnabled?: boolean
  createdAt: string
  updatedAt: string
}

// Authentication Requests
export interface LoginRequest {
  email: string
  password: string
  clientId: string
  clientSecret?: string
}

export interface RegisterRequest {
  email: string
  password: string
  metadata: Record<string, string | null>
}

export interface ForgotPasswordRequest {
  email: string
}

export interface ResetPasswordRequest {
  userId: string
  token: string
  password: string
}

export interface MfaChallengeRequest {
  method: string
  clientId: string
}

export interface MfaVerifyRequest {
  method: string
  code: string
  clientId: string
}

// Authentication Responses
export interface LoginResponse {
  message: string
  clientId: string
  requiresTwoFactor?: boolean
  methods?: string[]
  recovered?: boolean
}

export type PasskeySignupMode = 'passkey-assisted' | 'passwordless'

export interface PasskeyConfiguration {
  enabled: boolean
  usernameless: boolean
  conditionalUi: boolean
  userVerification: 'required'
  signupModes: PasskeySignupMode[]
  signupEmailVerificationRequired: boolean
}

export interface PasskeySummary {
  id: string
  name: string
  createdAt: string
  transports: string[]
  isBackupEligible: boolean
  isBackedUp: boolean
  concurrencyStamp: string
}

export interface PasskeySignupRequest {
  mode: PasskeySignupMode
  email: string
  metadata?: Record<string, string | null>
}

export interface PasskeyEmailConfirmation {
  draftId: string
  token: string
}

export interface PasskeySignupConfirmation {
  registrationMode: PasskeySignupMode
}

export interface PasskeyCompletionRequest {
  draftId: string
  name: string
  password?: string
}

export interface PasskeyRecoveryRequest {
  email: string
}

export interface PasskeyLoginOptions {
  mediation?: 'required' | 'conditional'
  signal?: AbortSignal
}

export interface TokenResponse {
  access_token: string
  refresh_token?: string
  expires_in: number
  token_type?: string
}

// Profile Schema
export interface ProfileSchemaField {
  name: string
  displayName: string
  required: boolean
  maxLength: number
  pattern?: string
}

export interface ProfileSchemaResponse {
  fields: ProfileSchemaField[]
}

// PKCE
export interface PKCEPair {
  verifier: string
  challenge: string
}

// Storage
export interface TokenStorage {
  getToken: () => string | null
  setToken: (token: string) => void
  getRefreshToken: () => string | null
  setRefreshToken: (token: string) => void
  clear: () => void
}

// Events
export type AuthEvent =
  | { type: 'login'; user: UserProfile }
  | { type: 'logout' }
  | { type: 'token-refresh'; token: string }
  | { type: 'error'; error: Error }

export interface UserPermissionsResponse {
  permissions: string[]
}

export interface PagedResult<T> {
  page: number
  pageSize: number
  totalCount: number
  items: T[]
}

// Admin API – Users
export interface AdminUserListQuery {
  page?: number
  pageSize?: number
  search?: string
  role?: string
  locked?: boolean
  deleted?: boolean
  sort?: string
}

export interface AdminUserSummary {
  id: string
  email: string | null
  displayName: string | null
  emailConfirmed: boolean
  isLockedOut: boolean
  createdAt: string
  mfaEnabled: boolean
  roles: string[]
  isDeleted: boolean
}

export type AdminUserListResponse = PagedResult<AdminUserSummary>

export interface AdminUserExternalLogin {
  provider: string
  displayName: string
  key: string
}

export interface AdminUserDetail {
  id: string
  email: string | null
  emailConfirmed: boolean
  displayName: string | null
  createdAt: string
  lockoutEnabled: boolean
  isLockedOut: boolean
  lockoutEnd: string | null
  twoFactorEnabled: boolean
  phoneNumberConfirmed: boolean
  phoneNumber: string | null
  metadata: Record<string, string | null>
  concurrencyStamp: string
  roles: string[]
  externalLogins: AdminUserExternalLogin[]
  authenticatorConfigured: boolean
  passkeyCount: number
  isDeleted: boolean
}

export interface AdminUserCreateRequest {
  email: string
  password?: string
  displayName?: string | null
  metadata?: Record<string, string | null>
  emailConfirmed?: boolean
  sendConfirmationEmail?: boolean
  sendPasswordResetEmail?: boolean
  roles?: string[]
}

export interface AdminUserCreateResponse {
  id: string
  email: string | null
  displayName: string | null
}

export interface AdminUserUpdateRequest {
  concurrencyStamp: string
  displayName?: string | null
  metadata?: Record<string, string | null>
  emailConfirmed?: boolean
  lockoutEnabled?: boolean
  lockoutEnd?: string | null
  twoFactorEnabled?: boolean
  phoneNumber?: string | null
  phoneNumberConfirmed?: boolean
}

export interface AdminUserLockRequest {
  minutes?: number
}

export interface AdminUserRolesResponse {
  roles: string[]
}

export interface AdminUserRolesUpdateRequest {
  roles?: string[]
}

// Admin API – Roles
export interface AdminRoleSummary {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  concurrencyStamp: string
  permissions: string[]
  userCount: number
}

export interface AdminRoleListQuery {
  page?: number
  pageSize?: number
  search?: string
  isSystemRole?: boolean
  sort?: string
}

export type AdminRoleListResponse = PagedResult<AdminRoleSummary>

export interface AdminRoleDetail {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  concurrencyStamp: string
  permissions: string[]
}

export interface AdminRoleCreateRequest {
  name: string
  description?: string | null
  isSystemRole?: boolean
  permissions?: string[]
}

export interface AdminRoleUpdateRequest {
  concurrencyStamp: string
  name?: string | null
  description?: string | null
  isSystemRole?: boolean
  permissions?: string[]
}

export interface AdminPermissionSummary {
  id: string
  name: string
  description: string | null
  roleCount: number
}

export interface AdminPermissionListQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string
}

export type AdminPermissionListResponse = PagedResult<AdminPermissionSummary>

// Admin API – Service Principals
export interface AdminServicePrincipalSummary {
  id: string
  displayName: string
  clientId: string
  isDisabled: boolean
  createdAt: string
  updatedAt: string
  concurrencyStamp: string
  roles: string[]
}

export type AdminServicePrincipalDetail = AdminServicePrincipalSummary

export interface AdminServicePrincipalListQuery {
  page?: number
  pageSize?: number
  search?: string
  disabled?: boolean
}

export type AdminServicePrincipalListResponse = PagedResult<AdminServicePrincipalSummary>
export interface AdminServicePrincipalCreateRequest { displayName: string }
export interface AdminServicePrincipalUpdateRequest { displayName: string; concurrencyStamp: string }
export interface AdminServicePrincipalRolesResponse { roles: string[] }
export interface AdminServicePrincipalCredential {
  id: string
  name: string
  createdAt: string
  expiresAt: string | null
  revokedAt: string | null
  revokedReason: string | null
}
export interface AdminServicePrincipalIssueCredentialRequest { name: string; expiresAt?: string | null }
export interface AdminServicePrincipalIssuedCredential {
  id: string
  name: string
  secret: string
  createdAt: string
  expiresAt: string | null
}

// Errors
export interface ApiError {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
  [key: string]: unknown
}

// Auth State
export interface AuthState {
  user: UserProfile | null
  isAuthenticated: boolean
  isLoading: boolean
  error: ApiError | null
}
