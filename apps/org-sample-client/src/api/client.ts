import { CONFIG } from '../config'
import { getAuthManager } from '../auth/manager'

interface ApiError {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
  [key: string]: unknown
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit & { parse?: 'json' | 'text' } = {},
): Promise<T> {
  const { parse = 'json', headers, ...init } = options

  const requestHeaders = new Headers(headers ?? {})
  if (!requestHeaders.has('Content-Type')) {
    requestHeaders.set('Content-Type', 'application/json')
  }

  const authManager = getAuthManager()
  if (authManager) {
    try {
      const token = await authManager.getAccessToken()
      if (token) {
        requestHeaders.set('Authorization', `Bearer ${token}`)
      }
    } catch {
      // Swallow token retrieval errors; the request will proceed unauthenticated.
    }
  }

  const response = await fetch(`${CONFIG.apiBase}${path}`, {
    credentials: 'include',
    headers: requestHeaders,
    ...init,
  })

  if (!response.ok) {
    let errorBody: ApiError | string | null = null
    try {
      errorBody = await response.json()
    } catch {
      errorBody = await response.text()
    }

    const error: ApiError = typeof errorBody === 'string' ? { detail: errorBody } : errorBody ?? {}
    error.status = response.status
    throw error
  }

  if (parse === 'text') {
    return (await response.text()) as unknown as T
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function renderApiError(error: unknown): string {
  if (typeof error === 'string') {
    return error
  }

  if (error && typeof error === 'object' && 'detail' in error) {
    return String(error.detail)
  }

  return 'Unexpected error occurred.'
}
