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

    // For now, we'll just return the token
    // In a production app, you'd want to check if it's expired
    // and automatically refresh it if needed
    return accessToken
  }

  isAuthenticated(): boolean {
    return !!this.getAccessToken()
  }
}