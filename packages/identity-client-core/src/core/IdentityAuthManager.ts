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
  AdminRoleListQuery,
  AdminRoleListResponse,
  AdminPermissionListQuery,
  AdminPermissionListResponse,
  AdminServicePrincipalListQuery,
  AdminServicePrincipalListResponse,
  AdminServicePrincipalSummary,
  AdminServicePrincipalDetail,
  AdminServicePrincipalCreateRequest,
  AdminServicePrincipalUpdateRequest,
  AdminServicePrincipalRolesResponse,
  AdminServicePrincipalCredential,
  AdminServicePrincipalIssueCredentialRequest,
  AdminServicePrincipalIssuedCredential,
  UserPermissionsResponse,
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
  public admin!: {
    users: {
      list: (query?: AdminUserListQuery) => Promise<AdminUserListResponse>
      get: (id: string) => Promise<AdminUserDetail>
      create: (payload: AdminUserCreateRequest) => Promise<AdminUserCreateResponse>
      update: (id: string, payload: AdminUserUpdateRequest) => Promise<AdminUserDetail>
      lock: (id: string, payload?: AdminUserLockRequest) => Promise<void>
      unlock: (id: string) => Promise<void>
      forcePasswordReset: (id: string) => Promise<void>
      resetMfa: (id: string) => Promise<void>
      resendConfirmation: (id: string) => Promise<void>
      getRoles: (id: string) => Promise<AdminUserRolesResponse>
      updateRoles: (id: string, payload: AdminUserRolesUpdateRequest) => Promise<void>
      softDelete: (id: string) => Promise<void>
      restore: (id: string) => Promise<void>
    }
    roles: {
      list: (query?: AdminRoleListQuery) => Promise<AdminRoleListResponse>
      create: (payload: AdminRoleCreateRequest) => Promise<AdminRoleDetail>
      update: (id: string, payload: AdminRoleUpdateRequest) => Promise<AdminRoleDetail>
      delete: (id: string) => Promise<void>
    }
    permissions: {
      list: (query?: AdminPermissionListQuery) => Promise<AdminPermissionListResponse>
    }
    servicePrincipals: {
      list: (query?: AdminServicePrincipalListQuery) => Promise<AdminServicePrincipalListResponse>
      get: (id: string) => Promise<AdminServicePrincipalDetail>
      create: (payload: AdminServicePrincipalCreateRequest) => Promise<AdminServicePrincipalSummary>
      update: (id: string, payload: AdminServicePrincipalUpdateRequest) => Promise<AdminServicePrincipalSummary>
      disable: (id: string, reason?: string) => Promise<void>
      restore: (id: string) => Promise<void>
      getRoles: (id: string) => Promise<AdminServicePrincipalRolesResponse>
      updateRoles: (id: string, roles: string[]) => Promise<AdminServicePrincipalRolesResponse>
      listCredentials: (id: string) => Promise<AdminServicePrincipalCredential[]>
      issueCredential: (id: string, payload: AdminServicePrincipalIssueCredentialRequest) => Promise<AdminServicePrincipalIssuedCredential>
      revokeCredential: (id: string, credentialId: string, reason?: string) => Promise<void>
      revokeAllCredentials: (id: string, reason?: string) => Promise<void>
    }
  }

  constructor(config: IdentityConfig) {
    this.config = config
    this.apiClient = new ApiClient(config)
    this.tokenManager = new TokenManager(config)
    this.pkceManager = new PKCEManager()

    // Explicit admin namespaces (parallel to org client)
    this.admin = {
      users: {
        list: async (query: AdminUserListQuery = {}): Promise<AdminUserListResponse> => {
          const params = new URLSearchParams()
          if (typeof query.page === 'number') params.set('page', String(query.page))
          if (typeof query.pageSize === 'number') params.set('pageSize', String(query.pageSize))
          if (typeof query.locked === 'boolean') params.set('locked', String(query.locked))
          if (query.search && query.search.trim().length > 0) params.set('search', query.search.trim())
          if (query.role && query.role.trim().length > 0) params.set('role', query.role.trim())
          if (typeof query.deleted === 'boolean') params.set('deleted', String(query.deleted))
          appendSortParam(params, query.sort)
          const qs = params.toString()
          const path = qs.length > 0 ? `/admin/users?${qs}` : '/admin/users'
          return await this.authorizedFetch<AdminUserListResponse>(path)
        },
        get: async (id: string): Promise<AdminUserDetail> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<AdminUserDetail>(`/admin/users/${encodedId}`)
        },
        create: async (payload: AdminUserCreateRequest): Promise<AdminUserCreateResponse> => {
          return await this.authorizedFetch<AdminUserCreateResponse>('/admin/users', { method: 'POST', body: JSON.stringify(payload) })
        },
        update: async (id: string, payload: AdminUserUpdateRequest): Promise<AdminUserDetail> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<AdminUserDetail>(`/admin/users/${encodedId}`, { method: 'PUT', body: JSON.stringify(payload) })
        },
        lock: async (id: string, payload?: AdminUserLockRequest): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/lock`, { method: 'POST', body: JSON.stringify(payload ?? {}) })
        },
        unlock: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/unlock`, { method: 'POST' })
        },
        forcePasswordReset: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/force-password-reset`, { method: 'POST' })
        },
        resetMfa: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/mfa/reset`, { method: 'POST' })
        },
        resendConfirmation: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/resend-confirmation`, { method: 'POST' })
        },
        getRoles: async (id: string): Promise<AdminUserRolesResponse> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<AdminUserRolesResponse>(`/admin/users/${encodedId}/roles`)
        },
        updateRoles: async (id: string, payload: AdminUserRolesUpdateRequest): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/roles`, { method: 'PUT', body: JSON.stringify(payload ?? { roles: [] }) })
        },
        softDelete: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}`, { method: 'DELETE' })
        },
        restore: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/users/${encodedId}/restore`, { method: 'POST' })
        },
      },
      roles: {
        list: async (query: AdminRoleListQuery = {}): Promise<AdminRoleListResponse> => {
          const params = new URLSearchParams()
          if (typeof query.page === 'number') params.set('page', String(query.page))
          if (typeof query.pageSize === 'number') params.set('pageSize', String(query.pageSize))
          if (query.search && query.search.trim().length > 0) params.set('search', query.search.trim())
          if (typeof query.isSystemRole === 'boolean') params.set('isSystemRole', String(query.isSystemRole))
          appendSortParam(params, query.sort)
          const qs = params.toString()
          const path = qs.length > 0 ? `/admin/roles?${qs}` : '/admin/roles'
          return await this.authorizedFetch<AdminRoleListResponse>(path)
        },
        create: async (payload: AdminRoleCreateRequest): Promise<AdminRoleDetail> => {
          return await this.authorizedFetch<AdminRoleDetail>('/admin/roles', { method: 'POST', body: JSON.stringify(payload) })
        },
        update: async (id: string, payload: AdminRoleUpdateRequest): Promise<AdminRoleDetail> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<AdminRoleDetail>(`/admin/roles/${encodedId}`, { method: 'PUT', body: JSON.stringify(payload) })
        },
        delete: async (id: string): Promise<void> => {
          const encodedId = encodeURIComponent(id)
          return await this.authorizedFetch<void>(`/admin/roles/${encodedId}`, { method: 'DELETE' })
        },
      },
      permissions: {
        list: async (query: AdminPermissionListQuery = {}): Promise<AdminPermissionListResponse> => {
          const params = new URLSearchParams()
          if (typeof query.page === 'number') params.set('page', String(query.page))
          if (typeof query.pageSize === 'number') params.set('pageSize', String(query.pageSize))
          if (query.search && query.search.trim().length > 0) params.set('search', query.search.trim())
          appendSortParam(params, query.sort)
          const qs = params.toString()
          const path = qs.length > 0 ? `/admin/permissions?${qs}` : '/admin/permissions'
          return await this.authorizedFetch<AdminPermissionListResponse>(path)
        },
      },
      servicePrincipals: {
        list: async (query: AdminServicePrincipalListQuery = {}): Promise<AdminServicePrincipalListResponse> => {
          const params = new URLSearchParams()
          if (typeof query.page === 'number') params.set('page', String(query.page))
          if (typeof query.pageSize === 'number') params.set('pageSize', String(query.pageSize))
          if (query.search?.trim()) params.set('search', query.search.trim())
          if (typeof query.disabled === 'boolean') params.set('disabled', String(query.disabled))
          const qs = params.toString()
          return await this.authorizedFetch<AdminServicePrincipalListResponse>(
            qs ? `/admin/service-principals?${qs}` : '/admin/service-principals')
        },
        get: async (id) => await this.authorizedFetch<AdminServicePrincipalDetail>(`/admin/service-principals/${encodeURIComponent(id)}`),
        create: async (payload) => await this.authorizedFetch<AdminServicePrincipalSummary>('/admin/service-principals', { method: 'POST', body: JSON.stringify(payload) }),
        update: async (id, payload) => await this.authorizedFetch<AdminServicePrincipalSummary>(`/admin/service-principals/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify(payload) }),
        disable: async (id, reason) => await this.authorizedFetch<void>(`/admin/service-principals/${encodeURIComponent(id)}/disable`, { method: 'POST', body: JSON.stringify({ reason }) }),
        restore: async (id) => await this.authorizedFetch<void>(`/admin/service-principals/${encodeURIComponent(id)}/restore`, { method: 'POST' }),
        getRoles: async (id) => await this.authorizedFetch<AdminServicePrincipalRolesResponse>(`/admin/service-principals/${encodeURIComponent(id)}/roles`),
        updateRoles: async (id, roles) => await this.authorizedFetch<AdminServicePrincipalRolesResponse>(`/admin/service-principals/${encodeURIComponent(id)}/roles`, { method: 'PUT', body: JSON.stringify({ roles }) }),
        listCredentials: async (id) => await this.authorizedFetch<AdminServicePrincipalCredential[]>(`/admin/service-principals/${encodeURIComponent(id)}/credentials`),
        issueCredential: async (id, payload) => await this.authorizedFetch<AdminServicePrincipalIssuedCredential>(`/admin/service-principals/${encodeURIComponent(id)}/credentials`, { method: 'POST', body: JSON.stringify(payload) }),
        revokeCredential: async (id, credentialId, reason) => await this.authorizedFetch<void>(`/admin/service-principals/${encodeURIComponent(id)}/credentials/${encodeURIComponent(credentialId)}/revoke`, { method: 'POST', body: JSON.stringify({ reason }) }),
        revokeAllCredentials: async (id, reason) => await this.authorizedFetch<void>(`/admin/service-principals/${encodeURIComponent(id)}/credentials/revoke-all`, { method: 'POST', body: JSON.stringify({ reason }) }),
      },
    }
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

    const headers: Record<string, string> = {
      ...(init.headers as Record<string, string> | undefined),
    }

    if (token) {
      headers.Authorization = `Bearer ${token}`
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
    return await this.authorizedFetch<{ sharedKey: string; authenticatorUri: string }>('/auth/mfa/enroll', {
      method: 'POST',
    })
  }

  async disableMfa(): Promise<{ message: string }> {
    return await this.authorizedFetch<{ message: string }>('/auth/mfa/disable', {
      method: 'POST',
    })
  }

  async regenerateRecoveryCodes(): Promise<{ recoveryCodes: string[] }> {
    return await this.authorizedFetch<{ recoveryCodes: string[] }>('/auth/mfa/recovery-codes', {
      method: 'POST',
    })
  }

  // Profile
  async getProfileSchema(): Promise<ProfileSchemaResponse> {
    return await this.apiClient.fetch<ProfileSchemaResponse>('/auth/profile-schema', {
      method: 'GET',
    })
  }

  async updateProfile(payload: { metadata: Record<string, string | null>; concurrencyStamp: string }): Promise<UserProfile> {
    return await this.authorizedFetch<UserProfile>('/users/me/profile', {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
  }

  async getUserPermissions(): Promise<string[]> {
    try {
      const response = await this.authorizedFetch<UserPermissionsResponse>('/users/me/permissions')
      if (!response || !Array.isArray(response.permissions)) {
        return []
      }
      return response.permissions
    } catch (error: any) {
      const normalized = createError(error)
      if (normalized.status === 404) {
        return []
      }

      throw normalized
    }
  }

  // Admin APIs moved under this.admin (breaking change)

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

  async refreshTokens(): Promise<UserProfile | null> {
    try {
      await this.tokenManager.refreshAccessToken()
    } catch (error) {
      debugLog('IdentityAuthManager.refreshTokens: refresh failed', error)
      throw error
    }

    const user = await this.getCurrentUser()
    if (user) {
      this.emit({ type: 'login', user })
    }

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
    const headers: Record<string, string> = {}
    if (token) {
      headers.Authorization = `Bearer ${token}`
    }

    return await this.apiClient.fetch<{ message: string }>(`/auth/external/${provider}`, {
      method: 'DELETE',
      headers,
    })
  }
}

function appendSortParam(params: URLSearchParams, sort?: string | string[]): void {
  if (!sort) {
    return
  }

  const values = Array.isArray(sort) ? sort : [sort]
  for (const value of values) {
    const trimmed = value?.trim()
    if (trimmed && trimmed.length > 0) {
      params.append('sort', trimmed)
    }
  }
}
