import { useState, useCallback } from 'react'
import type { LoginRequest, LoginResponse } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseLoginOptions {
  onSuccess?: (response: LoginResponse) => void
  onError?: (error: any) => void
}

export function useLogin(options: UseLoginOptions = {}) {
  const { authManager, refreshUser } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const login = useCallback(async (request: Omit<LoginRequest, 'clientId'>) => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.login({
        ...request,
        clientId: '',
      })

      // If login successful and no 2FA required, refresh user state
      if (response.message && !response.requiresTwoFactor) {
        await refreshUser()
      }

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
  }, [authManager, refreshUser, options])

  const logout = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      await authManager.logout()
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  return {
    login,
    logout,
    isLoading,
    error,
  }
}