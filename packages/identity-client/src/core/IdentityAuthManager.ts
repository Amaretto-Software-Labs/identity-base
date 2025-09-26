import type {
  IdentityConfig,
  UserProfile,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  MfaChallengeRequest,
  MfaVerifyRequest,
  ProfileSchemaResponse,
  AuthEvent,
  AuthState
} from './types'
import { ApiClient } from './ApiClient'
import { TokenManager } from './TokenManager'
import { PKCEManager, generatePkce, randomState } from '../utils/pkce'
import { createError } from '../utils/errors'

export class IdentityAuthManager {
  private config: IdentityConfig
  private apiClient: ApiClient
  private tokenManager: TokenManager
  private pkceManager: PKCEManager
  private eventListeners: Array<(event: AuthEvent) => void> = []

  constructor(config: IdentityConfig) {
    this.config = config
    this.apiClient = new ApiClient(config)
    this.tokenManager = new TokenManager(config)
    this.pkceManager = new PKCEManager()
  }

  // Event system
  addEventListener(callback: (event: AuthEvent) => void): () => void {
    this.eventListeners.push(callback)
    return () => {
      this.eventListeners = this.eventListeners.filter(cb => cb !== callback)
    }
  }

  private emit(event: AuthEvent): void {
    this.eventListeners.forEach(callback => {
      try {
        callback(event)
      } catch (error) {
        console.error('Error in auth event listener:', error)
      }
    })
  }

  // Authentication state
  isAuthenticated(): boolean {
    return this.tokenManager.isAuthenticated()
  }

  async getCurrentUser(): Promise<UserProfile | null> {
    try {
      const token = await this.tokenManager.ensureValidToken()
      if (!token) return null

      return await this.apiClient.fetch<UserProfile>('/users/me', {
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      })
    } catch (error: any) {
      if (error?.status === 401) {
        return null
      }
      throw error
    }
  }

  async getAccessToken(): Promise<string | null> {
    return await this.tokenManager.ensureValidToken()
  }

  // Login flow
  async login(request: LoginRequest): Promise<LoginResponse> {
    const loginRequest = {
      ...request,
      clientId: request.clientId || this.config.clientId,
    }

    const response = await this.apiClient.fetch<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(loginRequest),
    })

    // If login successful and no 2FA required, user is authenticated via cookie
    if (response.message && !response.requiresTwoFactor) {
      const user = await this.getCurrentUser()
      if (user) {
        this.emit({ type: 'login', user })
      }
    }

    return response
  }

  async logout(): Promise<void> {
    try {
      await this.apiClient.fetch<void>('/auth/logout', {
        method: 'POST',
      })
    } catch (error) {
      // Continue with logout even if API call fails
      console.warn('Logout API call failed:', error)
    } finally {
      this.tokenManager.clearTokens()
      this.pkceManager.clearPkce()
      this.emit({ type: 'logout' })
    }
  }

  // Registration
  async register(request: RegisterRequest): Promise<{ correlationId: string }> {
    return await this.apiClient.fetch<{ correlationId: string }>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(request),
    })
  }

  // MFA
  async sendMfaChallenge(request: MfaChallengeRequest): Promise<{ message: string }> {
    const mfaRequest = {
      ...request,
      clientId: request.clientId || this.config.clientId,
    }

    return await this.apiClient.fetch<{ message: string }>('/auth/mfa/challenge', {
      method: 'POST',
      body: JSON.stringify(mfaRequest),
    })
  }

  async verifyMfa(request: MfaVerifyRequest): Promise<{ message: string }> {
    const mfaRequest = {
      ...request,
      clientId: request.clientId || this.config.clientId,
    }

    return await this.apiClient.fetch<{ message: string }>('/auth/mfa/verify', {
      method: 'POST',
      body: JSON.stringify(mfaRequest),
    })
  }

  async enrollMfa(): Promise<{ sharedKey: string; authenticatorUri: string }> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    return await this.apiClient.fetch<{ sharedKey: string; authenticatorUri: string }>('/auth/mfa/enroll', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    })
  }

  async disableMfa(): Promise<{ message: string }> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    return await this.apiClient.fetch<{ message: string }>('/auth/mfa/disable', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    })
  }

  async regenerateRecoveryCodes(): Promise<{ recoveryCodes: string[] }> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    return await this.apiClient.fetch<{ recoveryCodes: string[] }>('/auth/mfa/recovery-codes', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    })
  }

  // Profile
  async getProfileSchema(): Promise<ProfileSchemaResponse> {
    return await this.apiClient.fetch<ProfileSchemaResponse>('/auth/profile-schema', {
      method: 'GET',
    })
  }

  async updateProfile(payload: { metadata: Record<string, string | null>; concurrencyStamp: string }): Promise<UserProfile> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    return await this.apiClient.fetch<UserProfile>('/users/me/profile', {
      method: 'PUT',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify(payload),
    })
  }

  // OAuth2 Authorization Code Flow
  async startAuthorization(): Promise<void> {
    const { challenge, verifier } = await generatePkce()
    const state = randomState()

    this.pkceManager.persistPkce(verifier, state)

    const authUrl = this.apiClient.buildAuthorizationUrl(challenge, state)
    window.location.assign(authUrl)
  }

  async handleAuthorizationCallback(code: string, state: string): Promise<UserProfile> {
    const verifier = this.pkceManager.consumePkce(state)
    if (!verifier) {
      throw createError('PKCE verifier not found. Authorization may have been started in a different session.')
    }

    const tokenResponse = await this.tokenManager.exchangeAuthorizationCode(code, verifier)

    this.emit({ type: 'token-refresh', token: tokenResponse.access_token })

    const user = await this.getCurrentUser()
    if (!user) {
      throw createError('Failed to get user profile after authentication')
    }

    this.emit({ type: 'login', user })
    return user
  }

  // External auth
  buildExternalStartUrl(
    provider: string,
    mode: 'login' | 'link',
    returnUrl: string,
    extras?: Record<string, string>,
  ): string {
    const params: Record<string, string> = {
      mode,
      returnUrl,
      ...extras,
    }

    return this.apiClient.buildUrl(`/auth/external/${provider}/start`, params)
  }

  async unlinkExternalProvider(provider: string): Promise<{ message: string }> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    return await this.apiClient.fetch<{ message: string }>(`/auth/external/${provider}`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    })
  }
}