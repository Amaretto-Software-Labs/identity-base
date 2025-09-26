export interface ProfileSchemaField {
  name: string
  displayName: string
  required: boolean
  maxLength: number
  pattern?: string | null
  type: string
}

export interface ProfileSchemaResponse {
  fields: ProfileSchemaField[]
}

export interface RegisterRequest {
  email: string
  password: string
  metadata: Record<string, string | null>
}

export interface LoginRequest {
  email: string
  password: string
  clientId: string
  clientSecret?: string
}

export interface LoginResponse {
  requiresTwoFactor?: boolean
  methods?: string[]
  clientId?: string
  message?: string
}

export interface MfaChallengeRequest {
  method: 'authenticator' | 'sms' | 'email' | 'recovery'
}

export interface MfaVerifyRequest {
  code: string
  method: 'authenticator' | 'sms' | 'email' | 'recovery'
  rememberMachine?: boolean
}

export interface UserProfile {
  id: string
  email: string | null
  emailConfirmed: boolean
  displayName: string | null
  metadata: Record<string, string | null>
  concurrencyStamp: string
  twoFactorEnabled?: boolean
}
