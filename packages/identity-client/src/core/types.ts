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