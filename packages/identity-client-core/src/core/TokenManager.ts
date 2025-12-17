import type { TokenStorage, TokenResponse, IdentityConfig } from './types'
import { ApiClient } from './ApiClient'
import { createTokenStorage } from '../utils/storage'

export class TokenManager {
  private storage: TokenStorage
  private apiClient: ApiClient
  private config: IdentityConfig
  private refreshPromise: Promise<string> | null = null

  constructor(config: IdentityConfig) {
    this.config = config
    this.storage = createTokenStorage(config.tokenStorage)
    this.apiClient = new ApiClient(config)
  }

  getAccessToken(): string | null {
    return this.storage.getToken()
  }

  getRefreshToken(): string | null {
    return this.storage.getRefreshToken()
  }

  setTokens(tokenResponse: TokenResponse): void {
    this.storage.setToken(tokenResponse.access_token)
    if (tokenResponse.refresh_token) {
      this.storage.setRefreshToken(tokenResponse.refresh_token)
    }
  }

  clearTokens(): void {
    this.storage.clear()
    this.refreshPromise = null
  }

  async exchangeAuthorizationCode(code: string, codeVerifier: string): Promise<TokenResponse> {
    const form = new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      code_verifier: codeVerifier,
      redirect_uri: this.config.redirectUri,
      client_id: this.config.clientId,
    })

    const tokenResponse = await this.apiClient.fetch<TokenResponse>('/connect/token', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body: form.toString(),
    })

    this.setTokens(tokenResponse)
    return tokenResponse
  }

  async refreshAccessToken(): Promise<string> {
    // Prevent multiple concurrent refresh requests
    if (this.refreshPromise) {
      return this.refreshPromise
    }

    const refreshToken = this.getRefreshToken()
    if (!refreshToken) {
      throw new Error('No refresh token available')
    }

    this.refreshPromise = this.performTokenRefresh(refreshToken)

    try {
      const accessToken = await this.refreshPromise
      return accessToken
    } finally {
      this.refreshPromise = null
    }
  }

  private async performTokenRefresh(refreshToken: string): Promise<string> {
    const form = new URLSearchParams({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: this.config.clientId,
    })

    const tokenResponse = await this.apiClient.fetch<TokenResponse>('/connect/token', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body: form.toString(),
    })

    this.setTokens(tokenResponse)
    return tokenResponse.access_token
  }

  async ensureValidToken(): Promise<string | null> {
    const accessToken = this.getAccessToken()
    if (!accessToken) {
      return null
    }

    const expiresAt = this.decodeTokenExpiry(accessToken)
    if (!expiresAt) {
      return accessToken
    }

    const now = Math.floor(Date.now() / 1000)
    const isExpired = expiresAt <= now
    const refreshThreshold = now + 30 // refresh 30s before expiry to avoid race conditions

    if (!this.config.autoRefresh) {
      if (isExpired) {
        this.clearTokens()
        return null
      }

      return accessToken
    }

    if (expiresAt > refreshThreshold) {
      return accessToken
    }

    try {
      return await this.refreshAccessToken()
    } catch (error) {
      this.clearTokens()
      throw error
    }
  }

  isAuthenticated(): boolean {
    return !!this.getAccessToken()
  }

  private decodeTokenExpiry(token: string): number | null {
    const parts = token.split('.')
    if (parts.length !== 3) {
      return null
    }

    try {
      const payload = JSON.parse(this.base64UrlDecode(parts[1])) as { exp?: unknown }
      return typeof payload.exp === 'number' ? payload.exp : null
    } catch {
      return null
    }
  }

  private base64UrlDecode(segment: string): string {
    const normalized = segment.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=')

    if (typeof window === 'undefined') {
      const globalBuffer = typeof globalThis !== 'undefined' ? (globalThis as any).Buffer : undefined
      if (globalBuffer?.from) {
        return globalBuffer.from(padded, 'base64').toString('utf-8')
      }
    }

    return decodeURIComponent(
      Array.prototype.map
        .call(atob(padded), (c: string) => `%${`00${c.charCodeAt(0).toString(16)}`.slice(-2)}`)
        .join(''),
    )
  }
}

