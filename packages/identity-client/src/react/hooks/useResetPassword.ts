import { useCallback, useState } from 'react'
import type { ResetPasswordRequest } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseResetPasswordOptions {
  onSuccess?: (message?: string) => void
  onError?: (error: any) => void
}

export function useResetPassword(options: UseResetPasswordOptions = {}) {
  const { authManager } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)
  const [isCompleted, setIsCompleted] = useState(false)

  const resetPassword = useCallback(async (request: ResetPasswordRequest) => {
    setIsLoading(true)
    setError(null)
    setIsCompleted(false)

    try {
      const response = await authManager.resetPassword(request)
      setIsCompleted(true)
      options.onSuccess?.(response?.message)
      return response
    } catch (err) {
      const formatted = createError(err)
      setError(formatted)
      options.onError?.(formatted)
      throw formatted
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  return {
    resetPassword,
    isLoading,
    isCompleted,
    error,
  }
}
