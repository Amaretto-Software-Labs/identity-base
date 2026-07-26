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
    const rawBody = await response.text()
    const errorBody = parseErrorBody(rawBody)

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

function parseErrorBody(rawBody: string): ApiError | string | null {
  if (!rawBody) {
    return null
  }

  try {
    return JSON.parse(rawBody) as ApiError
  } catch {
    return rawBody
  }
}

export function buildUrl(path: string, params: Record<string, string | number | boolean | undefined>) {
  const url = new URL(`${CONFIG.apiBase}${path}`, window.location.origin)
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null) return
    url.searchParams.append(key, String(value))
  })
  return url.toString()
}
