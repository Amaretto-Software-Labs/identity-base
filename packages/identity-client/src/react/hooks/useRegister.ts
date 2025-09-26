import { useState, useCallback } from 'react'
import type { RegisterRequest } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseRegisterOptions {
  onSuccess?: (response: { correlationId: string }) => void
  onError?: (error: any) => void
}

export function useRegister(options: UseRegisterOptions = {}) {
  const { authManager } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const register = useCallback(async (request: RegisterRequest) => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.register(request)
      options.onSuccess?.(response)
      return response
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  return {
    register,
    isLoading,
    error,
  }
}