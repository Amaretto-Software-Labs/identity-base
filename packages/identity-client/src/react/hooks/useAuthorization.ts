import { useState, useCallback, useRef } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseAuthorizationOptions {
  onSuccess?: () => void
  onError?: (error: any) => void
}

export function useAuthorization(options: UseAuthorizationOptions = {}) {
  const { authManager } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  // Use ref to store latest options without causing re-renders
  const optionsRef = useRef(options)
  optionsRef.current = options

  const startAuthorization = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      await authManager.startAuthorization()
      // Note: This will redirect the browser, so code after this won't execute
    } catch (err) {
      const error = createError(err)
      setError(error)
      optionsRef.current.onError?.(error)
      setIsLoading(false)
    }
  }, [authManager])

  const handleCallback = useCallback(async (code: string, state: string) => {
    setIsLoading(true)
    setError(null)

    try {
      const user = await authManager.handleAuthorizationCallback(code, state)
      optionsRef.current.onSuccess?.()
      return user
    } catch (err) {
      const error = createError(err)
      setError(error)
      optionsRef.current.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager])

  return {
    startAuthorization,
    handleCallback,
    isLoading,
    error,
  }
}