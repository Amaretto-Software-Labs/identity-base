import { CONFIG } from '../config'

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
  const response = await fetch(`${CONFIG.apiBase}${path}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...headers,
    },
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

