import { useCallback, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseForgotPasswordOptions {
  onSuccess?: () => void
  onError?: (error: any) => void
}

export function useForgotPassword(options: UseForgotPasswordOptions = {}) {
  const { authManager } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)
  const [isCompleted, setIsCompleted] = useState(false)

  const requestReset = useCallback(async (email: string) => {
    setIsLoading(true)
    setError(null)
    setIsCompleted(false)

    try {
      await authManager.requestPasswordReset(email)
      setIsCompleted(true)
      options.onSuccess?.()
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
    requestReset,
    isLoading,
    isCompleted,
    error,
  }
}
