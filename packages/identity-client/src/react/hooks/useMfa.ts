import { useState, useCallback } from 'react'
import type { MfaChallengeRequest, MfaVerifyRequest } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseMfaOptions {
  onChallengeSuccess?: (response: { message: string }) => void
  onVerifySuccess?: (response: { message: string }) => void
  onError?: (error: any) => void
}

export function useMfa(options: UseMfaOptions = {}) {
  const { authManager, refreshUser } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const sendChallenge = useCallback(async (request: Omit<MfaChallengeRequest, 'clientId'>) => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.sendMfaChallenge({
        ...request,
        clientId: '',
      })

      options.onChallengeSuccess?.(response)
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

  const verifyChallenge = useCallback(async (request: Omit<MfaVerifyRequest, 'clientId'>) => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.verifyMfa({
        ...request,
        clientId: '',
      })

      // After successful MFA verification, refresh user state
      await refreshUser()

      options.onVerifySuccess?.(response)
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

  return {
    sendChallenge,
    verifyChallenge,
    isLoading,
    error,
  }
}