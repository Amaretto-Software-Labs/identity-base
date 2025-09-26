import type { TokenStorage } from '../core/types'

export class LocalStorageTokenStorage implements TokenStorage {
  private tokenKey = 'identity:access_token'
  private refreshTokenKey = 'identity:refresh_token'

  getToken(): string | null {
    try {
      return localStorage.getItem(this.tokenKey)
    } catch {
      return null
    }
  }

  setToken(token: string): void {
    try {
      localStorage.setItem(this.tokenKey, token)
    } catch {
      // Silently fail if localStorage is not available
    }
  }

  getRefreshToken(): string | null {
    try {
      return localStorage.getItem(this.refreshTokenKey)
    } catch {
      return null
    }
  }

  setRefreshToken(token: string): void {
    try {
      localStorage.setItem(this.refreshTokenKey, token)
    } catch {
      // Silently fail if localStorage is not available
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(this.tokenKey)
      localStorage.removeItem(this.refreshTokenKey)
    } catch {
      // Silently fail if localStorage is not available
    }
  }
}

export class SessionStorageTokenStorage implements TokenStorage {
  private tokenKey = 'identity:access_token'
  private refreshTokenKey = 'identity:refresh_token'

  getToken(): string | null {
    try {
      return sessionStorage.getItem(this.tokenKey)
    } catch {
      return null
    }
  }

  setToken(token: string): void {
    try {
      sessionStorage.setItem(this.tokenKey, token)
    } catch {
      // Silently fail if sessionStorage is not available
    }
  }

  getRefreshToken(): string | null {
    try {
      return sessionStorage.getItem(this.refreshTokenKey)
    } catch {
      return null
    }
  }

  setRefreshToken(token: string): void {
    try {
      sessionStorage.setItem(this.refreshTokenKey, token)
    } catch {
      // Silently fail if sessionStorage is not available
    }
  }

  clear(): void {
    try {
      sessionStorage.removeItem(this.tokenKey)
      sessionStorage.removeItem(this.refreshTokenKey)
    } catch {
      // Silently fail if sessionStorage is not available
    }
  }
}

export class MemoryTokenStorage implements TokenStorage {
  private token: string | null = null
  private refreshToken: string | null = null

  getToken(): string | null {
    return this.token
  }

  setToken(token: string): void {
    this.token = token
  }

  getRefreshToken(): string | null {
    return this.refreshToken
  }

  setRefreshToken(token: string): void {
    this.refreshToken = token
  }

  clear(): void {
    this.token = null
    this.refreshToken = null
  }
}

export function createTokenStorage(type: 'localStorage' | 'sessionStorage' | 'memory' = 'localStorage'): TokenStorage {
  switch (type) {
    case 'localStorage':
      return new LocalStorageTokenStorage()
    case 'sessionStorage':
      return new SessionStorageTokenStorage()
    case 'memory':
      return new MemoryTokenStorage()
    default:
      return new LocalStorageTokenStorage()
  }
}