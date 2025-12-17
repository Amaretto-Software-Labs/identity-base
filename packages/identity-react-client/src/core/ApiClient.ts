import type { IdentityConfig, ApiError } from './types'
import { createError } from '../utils/errors'

export class ApiClient {
  private config: IdentityConfig

  constructor(config: IdentityConfig) {
    this.config = config
  }

  async fetch<T>(
    path: string,
    options: RequestInit & { parse?: 'json' | 'text' } = {},
  ): Promise<T> {
    const { parse = 'json', headers, ...init } = options

    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeout || 10000)

    try {
      const response = await fetch(`${this.config.apiBase}${path}`, {
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          ...headers,
        },
        signal: controller.signal,
        ...init,
      })

      clearTimeout(timeoutId)

      if (!response.ok) {
        let errorBody: ApiError | string | null = null

        try {
          errorBody = await response.json()
        } catch {
          errorBody = await response.text()
        }

        const error: ApiError = typeof errorBody === 'string' ? { detail: errorBody } : errorBody ?? {}
        error.status = response.status
        throw createError(error)
      }

      if (parse === 'text') {
        return (await response.text()) as unknown as T
      }

      if (response.status === 204) {
        return undefined as T
      }

      const raw = await response.text()
      if (!raw) {
        return undefined as T
      }

      try {
        return JSON.parse(raw) as T
      } catch {
        throw createError({
          status: response.status,
          detail: raw,
        })
      }
    } catch (error: any) {
      clearTimeout(timeoutId)
      if (error?.name === 'AbortError') {
        throw createError('Request timeout')
      }
      throw createError(error)
    }
  }

  buildUrl(path: string, params: Record<string, string | number | boolean | undefined>): string {
    const url = new URL(`${this.config.apiBase}${path}`)
    Object.entries(params).forEach(([key, value]: [string, any]) => {
      if (value === undefined || value === null) return
      url.searchParams.append(key, String(value))
    })
    return url.toString()
  }

  buildAuthorizationUrl(codeChallenge: string, state: string): string {
    const params = new URLSearchParams({
      response_type: 'code',
      client_id: this.config.clientId,
      redirect_uri: this.config.redirectUri,
      scope: this.config.scope || 'openid profile email offline_access identity.api',
      code_challenge: codeChallenge,
      code_challenge_method: 'S256',
      state,
    })

    return `${this.config.apiBase}/connect/authorize?${params.toString()}`
  }
}
