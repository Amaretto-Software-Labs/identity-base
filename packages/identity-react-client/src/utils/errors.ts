import type { ApiError } from '../core/types'

export class IdentityError extends Error {
  public readonly status?: number
  public readonly errors?: Record<string, string[]>

  constructor(error: ApiError | string) {
    if (typeof error === 'string') {
      super(error)
    } else {
      super(error.detail || error.title || 'An error occurred')
      this.status = error.status
      this.errors = error.errors
    }
    this.name = 'IdentityError'
  }
}

export function createError(error: unknown): IdentityError {
  if (error instanceof IdentityError) {
    return error
  }

  if (error instanceof Error) {
    return new IdentityError(error.message || 'An unexpected error occurred')
  }

  if (typeof error === 'string') {
    return new IdentityError(error)
  }

  if (typeof error === 'object' && error !== null) {
    const apiError = error as ApiError
    if (apiError.errors) {
      const messages = Object.entries(apiError.errors)
        .map(([key, messages]) => `${key}: ${messages.join(', ')}`)
        .join('\n')
      return new IdentityError({ ...apiError, detail: messages })
    }
    return new IdentityError(apiError)
  }

  return new IdentityError('An unexpected error occurred')
}
