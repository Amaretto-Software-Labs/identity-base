import type {
  IdentityConfig,
  UserProfile,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  MfaChallengeRequest,
  MfaVerifyRequest,
  ProfileSchemaResponse,
  AuthEvent,
  AdminUserListQuery,
  AdminUserListResponse,
  AdminUserCreateRequest,
  AdminUserCreateResponse,
  AdminUserUpdateRequest,
  AdminUserDetail,
  AdminUserLockRequest,
  AdminUserRolesResponse,
  AdminUserRolesUpdateRequest,
  AdminRoleSummary,
  AdminRoleDetail,
  AdminRoleCreateRequest,
  AdminRoleUpdateRequest,
} from './types'
import { ApiClient } from './ApiClient'
import { TokenManager } from './TokenManager'
import { PKCEManager, generatePkce, randomState } from '../utils/pkce'
import { createError } from '../utils/errors'
import { debugLog } from '../utils/logger'

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

  private async authorizedFetch<T>(
    path: string,
    init: RequestInit & { parse?: 'json' | 'text' } = {},
  ): Promise<T> {
    const token = await this.tokenManager.ensureValidToken()
    if (!token) {
      throw createError('Authentication required')
    }

    const headers: Record<string, string> = {
      ...(init.headers as Record<string, string> | undefined),
      Authorization: `Bearer ${token}`,
    }

    return await this.apiClient.fetch<T>(path, {
      ...init,
      headers,
    })
  }

  // Authentication state
  isAuthenticated(): boolean {
    // For now, we return true if tokens exist (OAuth2 flow) or if we can't determine
    // the state from tokens alone (cookie-based auth requires a server call)
    return this.tokenManager.isAuthenticated()
  }

  async getCurrentUser(): Promise<UserProfile | null> {
    try {
      const token = await this.tokenManager.ensureValidToken()

      if (token) {
        debugLog('Using Bearer token authentication for /users/me')
        // Use Bearer token authentication (OAuth2 flow)
        return await this.apiClient.fetch<UserProfile>('/users/me', {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        })
      } else {
        debugLog('Using cookie-based authentication for /users/me')
        // Fall back to cookie-based authentication (direct login flow)
        return await this.apiClient.fetch<UserProfile>('/users/me', {
          method: 'GET',
        })
      }
    } catch (error: any) {
      debugLog('getCurrentUser error:', error)
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

  async requestPasswordReset(email: string): Promise<void> {
    const payload: ForgotPasswordRequest = { email }

    await this.apiClient.fetch('/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  }

  async resetPassword(request: ResetPasswordRequest): Promise<{ message: string }> {
    return await this.apiClient.fetch<{ message: string }>('/auth/reset-password', {
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

  // Admin APIs – Users
  async listAdminUsers(query: AdminUserListQuery = {}): Promise<AdminUserListResponse> {
    const params = new URLSearchParams()

    if (typeof query.page === 'number') {
      params.set('page', String(query.page))
    }

    if (typeof query.pageSize === 'number') {
      params.set('pageSize', String(query.pageSize))
    }

    if (typeof query.locked === 'boolean') {
      params.set('locked', String(query.locked))
    }

    if (query.search && query.search.trim().length > 0) {
      params.set('search', query.search.trim())
    }

    if (query.role && query.role.trim().length > 0) {
      params.set('role', query.role.trim())
    }

    const queryString = params.toString()
    const path = queryString.length > 0 ? `/admin/users?${queryString}` : '/admin/users'
    return await this.authorizedFetch<AdminUserListResponse>(path)
  }

  async getAdminUser(id: string): Promise<AdminUserDetail> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<AdminUserDetail>(`/admin/users/${encodedId}`)
  }

  async createAdminUser(payload: AdminUserCreateRequest): Promise<AdminUserCreateResponse> {
    return await this.authorizedFetch<AdminUserCreateResponse>('/admin/users', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  }

  async updateAdminUser(id: string, payload: AdminUserUpdateRequest): Promise<AdminUserDetail> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<AdminUserDetail>(`/admin/users/${encodedId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
  }

  async lockAdminUser(id: string, payload?: AdminUserLockRequest): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/lock`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    })
  }

  async unlockAdminUser(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/unlock`, {
      method: 'POST',
    })
  }

  async forceAdminPasswordReset(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/force-password-reset`, {
      method: 'POST',
    })
  }

  async resetAdminUserMfa(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/mfa/reset`, {
      method: 'POST',
    })
  }

  async resendAdminConfirmation(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/resend-confirmation`, {
      method: 'POST',
    })
  }

  async getAdminUserRoles(id: string): Promise<AdminUserRolesResponse> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<AdminUserRolesResponse>(`/admin/users/${encodedId}/roles`)
  }

  async updateAdminUserRoles(id: string, payload: AdminUserRolesUpdateRequest): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/roles`, {
      method: 'PUT',
      body: JSON.stringify(payload ?? { roles: [] }),
    })
  }

  async softDeleteAdminUser(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}`, {
      method: 'DELETE',
    })
  }

  async restoreAdminUser(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/users/${encodedId}/restore`, {
      method: 'POST',
    })
  }

  // Admin APIs – Roles
  async listAdminRoles(): Promise<AdminRoleSummary[]> {
    return await this.authorizedFetch<AdminRoleSummary[]>('/admin/roles')
  }

  async createAdminRole(payload: AdminRoleCreateRequest): Promise<AdminRoleDetail> {
    return await this.authorizedFetch<AdminRoleDetail>('/admin/roles', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  }

  async updateAdminRole(id: string, payload: AdminRoleUpdateRequest): Promise<AdminRoleDetail> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<AdminRoleDetail>(`/admin/roles/${encodedId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
  }

  async deleteAdminRole(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id)
    return await this.authorizedFetch<void>(`/admin/roles/${encodedId}`, {
      method: 'DELETE',
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
